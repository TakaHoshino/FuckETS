using System.IO;
using System.Windows;
using System.Windows.Controls;
using FuckETS.Services;
using Microsoft.Win32;

namespace FuckETS.Pages;

/// <summary>阶段 2：选择获取 E听说 安装目录的方式（自动 / 手动）。</summary>
public partial class OobeInstallSourcePage : OobePageBase
{
    private bool _applyingMode;

    public OobeInstallSourcePage()
    {
        InitializeComponent();
        PathBox.TextChanged += (_, _) => RecomputeCanProceed();

        // 在元素全部创建、OnInitialized 之后再挂接 Checked 事件，避免 XAML 解析期触发、ManualPanel 尚不存在。
        AutoRadio.Checked += Radio_Checked;
        ManualRadio.Checked += Radio_Checked;
    }

    protected override void OnInitialized()
    {
        Logger.Debug($"阶段 2 初始化：注册表中记录的获取方式为 {Session.Mode}。");
        var mode = Session.Mode == OobeStateService.AcquisitionMode.NotSet
            ? OobeStateService.AcquisitionMode.Auto
            : Session.Mode;
        ApplyMode(mode, restorePath: true);
    }

    /// <summary>集中应用所选获取方式：更新单选按钮选中态、显隐手动面板、刷新可继续状态。</summary>
    private void ApplyMode(OobeStateService.AcquisitionMode mode, bool restorePath)
    {
        if (_applyingMode)
            return; // 防止 Checked 事件重入
        _applyingMode = true;
        try
        {
            Session.Mode = mode;
            AutoRadio.IsChecked = mode == OobeStateService.AcquisitionMode.Auto;
            ManualRadio.IsChecked = mode == OobeStateService.AcquisitionMode.Manual;
            ManualPanel.Visibility = mode == OobeStateService.AcquisitionMode.Manual
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (restorePath && mode == OobeStateService.AcquisitionMode.Manual)
                PathBox.Text = Session.InstallDir;
            RecomputeCanProceed();
        }
        finally
        {
            _applyingMode = false;
        }
    }

    private void Radio_Checked(object sender, RoutedEventArgs e)
    {
        var isManual = ReferenceEquals(sender, ManualRadio);
        ApplyMode(isManual
            ? OobeStateService.AcquisitionMode.Manual
            : OobeStateService.AcquisitionMode.Auto, restorePath: false);
        Logger.Debug(isManual
            ? "用户选择“手动输入”E听说 安装目录。"
            : "用户选择“自动获取”E听说 安装目录。");
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 E听说 安装根目录",
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            PathBox.Text = dialog.FolderName;
            Logger.Info($"用户通过浏览选择了安装目录：{dialog.FolderName}");
        }
    }

    private void RecomputeCanProceed()
    {
        if (Session.Mode == OobeStateService.AcquisitionMode.Auto)
        {
            CanProceed = true;
            return;
        }

        if (Session.Mode == OobeStateService.AcquisitionMode.Manual)
        {
            var path = PathBox.Text?.Trim();
            CanProceed = !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }
        else
        {
            CanProceed = false;
        }
    }

    public override bool ValidateAndCommit()
    {
        // 自动方式无需额外校验
        if (Session.Mode == OobeStateService.AcquisitionMode.Auto)
        {
            Session.InstallDir = string.Empty;
            Logger.Info("已选择“自动获取”，进入等待并启动 E听说 阶段。");
            return true;
        }

        var path = PathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            Logger.Warn("阶段 2 校验：手动路径为空。");
            MessageBox.Show("请输入或浏览选择 E听说 软件的安装根目录。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!Directory.Exists(path))
        {
            Logger.Warn($"阶段 2 校验：手动路径不存在：{path}");
            MessageBox.Show($"路径不存在：{path}\n请选择有效的 E听说 安装目录。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PathBox.Focus();
            return false;
        }

        Session.InstallDir = path;
        OobeStateService.SaveInstallInfo(path, OobeStateService.AcquisitionMode.Manual);
        OobeStateService.SaveStage(OobeStateService.OobeStage.LaunchEts);
        Logger.Info($"已确认手动安装目录：{path}");
        return true;
    }
}