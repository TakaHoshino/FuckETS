using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using FuckETS.Models;
using FuckETS.Pages;
using FuckETS.Services;

namespace FuckETS;

/// <summary>OOBE 出厂初始设置向导窗口。</summary>
public partial class OobeWindow : Window
{
    private readonly OobeSession _session = new();
    private readonly Type[] _pageSequence =
    {
        typeof(OobeDisclaimerPage),
        typeof(OobeInstallSourcePage),
        typeof(OobeLaunchEtsPage),
        typeof(OobeCompletePage),
    };

    private int _currentIndex;
    private bool _completed;

    public OobeWindow()
    {
        InitializeComponent();
        Loaded += OobeWindow_Loaded;
    }

    /// <summary>窗口已显示后再启动首个页面，确保 DispatcherTimer 等可正常泵起。</summary>
    private void OobeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialNavigationDone)
            return;
        _initialNavigationDone = true;
        BeginCore();
    }

    private bool _initialNavigationDone;

    /// <summary>从注册表已保存的进度确定起始阶段并导航。</summary>
    public void Begin()
    {
        // 若窗口已显示（由 Loaded 触发），立即执行；否则交给 Loaded。
        if (_initialNavigationDone)
            return;
    }

    private void BeginCore()
    {
        var stage = OobeStateService.GetCurrentStage();
        var startIndex = stage switch
        {
            OobeStateService.OobeStage.Complete => 3,
            OobeStateService.OobeStage.LaunchEts => 2,
            OobeStateService.OobeStage.InstallSource => 1,
            _ => 0,
        };

        // 恢复会话状态
        _session.DisclaimerAccepted = stage >= OobeStateService.OobeStage.InstallSource;
        var mode = GetModeFromRegistry();
        _session.Mode = mode;
        var dir = OobeStateService.GetInstallDir();
        if (mode != OobeStateService.AcquisitionMode.NotSet)
            _session.InstallDir = dir ?? string.Empty;

        // 续做状态校验：若已到阶段2及以后但“获取方式”缺失/非法，说明状态残缺，回退到阶段1重新开始，
        // 避免因续做状态不一致导致向导卡死。
        if (startIndex >= 1 && mode == OobeStateService.AcquisitionMode.NotSet)
        {
            Logger.Warn($"OOBE 续做状态残缺（Stage={stage} 但 AcquisitionMode={mode}），回退到阶段1。");
            startIndex = 0;
            _session.Mode = OobeStateService.AcquisitionMode.NotSet;
            _session.DisclaimerAccepted = false;
        }

        _session.Resuming = startIndex > 0;

        _currentIndex = startIndex;
        Logger.Debug($"OOBE 从阶段 {stage} 开始（index={startIndex}，方式={mode}）。");
        NavigateTo(_currentIndex);
    }

    private OobeStateService.AcquisitionMode GetModeFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\FuckETS", writable: false);
            var v = key?.GetValue("AcquisitionMode")?.ToString();
            return v switch
            {
                "auto" => OobeStateService.AcquisitionMode.Auto,
                "manual" => OobeStateService.AcquisitionMode.Manual,
                _ => OobeStateService.AcquisitionMode.NotSet,
            };
        }
        catch
        {
            return OobeStateService.AcquisitionMode.NotSet;
        }
    }

    private void NavigateTo(int index)
    {
        // 通知上一页面清理（如停止计时器）
        if (ContentFrame.Content is OobePageBase oldPage)
            oldPage.OnPageClosed();

        _currentIndex = index;
        var pageType = _pageSequence[index];
        OobePageBase page;
        try
        {
            page = (OobePageBase)Activator.CreateInstance(pageType)!;
            Logger.Debug($"OOBE 导航到阶段 index={index}（页面 {pageType.Name}）。");

            // 先订阅再初始化，避免 Initialize 同步触发的事件丢失
            page.CanProceedChanged -= Page_CanProceedChanged;
            page.CanProceedChanged += Page_CanProceedChanged;

            page.Initialize(_session, this);
        }
        catch (Exception ex)
        {
            Logger.Error($"OOBE 页面创建/初始化失败（{pageType.Name}）：{ex.Message}", ex);
            MessageBox.Show(
                $"OOBE 页面初始化出错：{ex.Message}{Environment.NewLine}{Environment.NewLine}请重新启动应用。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ContentFrame.Content = page;

        UpdateHeader();
        // 事件订阅之后，用当前状态同步按钮；若无事件触发则以此兜底
        NextButton.IsEnabled = page.CanProceed;
        UpdateNavigationButtons();
    }

    private void Page_CanProceedChanged(object? sender, bool canProceed)
    {
        NextButton.IsEnabled = canProceed;
        Logger.Debug($"OOBE 页面“下一步”可用状态变化：{canProceed}。");
    }

    private void UpdateHeader()
    {
        var stage = _currentIndex + 1;
        StepText.Text = $"步骤 {stage} / {_pageSequence.Length}";
        StepProgress.Value = stage;
        TitleText.Text = _currentIndex switch
        {
            0 => "欢迎使用 FuckETS",
            1 => "选择获取 E听说 客户端的安装目录",
            2 => "等待并启动 E听说 客户端",
            _ => "完成设置",
        };
    }

    private void UpdateNavigationButtons()
    {
        // 阶段 1 之前（步骤0）无上一步
        PrevButton.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 阶段 4 为完成页，Next 变为“完成”
        NextButton.Content = _currentIndex == _pageSequence.Length - 1 ? "完成" : "下一步";
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            // 手动模式：完成页的上一步回到“选择获取方式”而非“等待并启动”
            var prevIndex = _currentIndex - 1;
            if (_currentIndex == _pageSequence.Length - 1 &&
                _session.Mode == OobeStateService.AcquisitionMode.Manual)
            {
                prevIndex = 1;
            }
            Logger.Debug($"OOBE 用户点击“上一步”，从 {_currentIndex} 回到 {prevIndex}。");
            NavigateTo(prevIndex);
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is OobePageBase page)
        {
            if (!page.ValidateAndCommit())
            {
                Logger.Debug($"OOBE 页面 {page.GetType().Name} 校验未通过，阻止下一步。");
                return;
            }
        }

        if (_currentIndex == _pageSequence.Length - 1)
        {
            // 完成：写入注册表并进入主界面
            Logger.Info("OOBE 完成页：正在写入注册表完成状态…");
            var ok = OobeStateService.CompleteOobe(_session.InstallDir, _session.Mode);
            _completed = true;
            if (!ok)
            {
                Logger.Error("OOBE 完成状态写入注册表失败。");
                MessageBox.Show(
                    "无法写入注册表配置，请检查权限后重试。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Logger.Info("OOBE 完成状态写入成功，关闭向导。");
            DialogResult = true;
            Close();
            return;
        }

        // 手动获取时跳过“等待并启动 E听说”阶段
        var nextIndex = _currentIndex + 1;
        if (_currentIndex == 1 && _session.Mode == OobeStateService.AcquisitionMode.Manual)
        {
            nextIndex = _pageSequence.Length - 1; // 跳到完成页
        }

        Logger.Debug($"OOBE 用户点击“下一步”，从 {_currentIndex} 到 {nextIndex}。");
        NavigateTo(nextIndex);
    }

    /// <summary>供各阶段页面调用：向会话提交状态并允许进入下一步。</summary>
    public OobeSession Session => _session;

    /// <summary>供阶段页面请求“直接完成”（如已检测到安装目录）。</summary>
    public void Advance()
    {
        if (_completed)
            return;
        NavigateTo(System.Math.Min(_currentIndex + 1, _pageSequence.Length - 1));
    }
}