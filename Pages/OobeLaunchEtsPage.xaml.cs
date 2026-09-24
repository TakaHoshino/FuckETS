using System.Windows;
using System.Windows.Threading;
using FuckETS.Services;

namespace FuckETS.Pages;

/// <summary>阶段 3：自动获取时等待并启动 E听说（轮询进程推导安装目录）。</summary>
public partial class OobeLaunchEtsPage : OobePageBase
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly DispatcherTimer _pollTimer;
    private bool _found;

    public OobeLaunchEtsPage()
    {
        InitializeComponent();
        _pollTimer = new DispatcherTimer { Interval = PollInterval };
        _pollTimer.Tick += PollTimer_Tick;
    }

    protected override void OnInitialized()
    {
        CanProceed = false;
        StartingPoll();
    }

    public override void OnPageClosed()
    {
        _pollTimer.Stop();
    }

    private void StartingPoll()
    {
        WaitingPanel.Visibility = Visibility.Visible;
        FoundPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "等待 E听说 启动…";
        CanProceed = false;
        Logger.Debug("阶段 3：开始轮询检测 E听说 进程。");
        CheckNow();
        _pollTimer.Start();
    }

    private void PollTimer_Tick(object? sender, EventArgs e) => CheckNow();

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        Logger.Debug("阶段 3：用户手动点击“重新检测”。");
        CheckNow();
    }

    private void CheckNow()
    {
        var root = EtsInstallService.DetachInstallRootFromProcess();
        if (string.IsNullOrEmpty(root))
            return; // 尚未检测到，继续等待

        _found = true;
        _pollTimer.Stop();
        Session.InstallDir = root;
        Session.Mode = OobeStateService.AcquisitionMode.Auto;
        Logger.Info($"阶段 3：已检测到 E听说 运行，推导安装目录={root}");

        WaitingPanel.Visibility = Visibility.Collapsed;
        FoundPanel.Visibility = Visibility.Visible;
        FoundDirText.Text = root;
        CanProceed = true;

        OobeStateService.SaveInstallInfo(root, OobeStateService.AcquisitionMode.Auto);
        OobeStateService.SaveStage(OobeStateService.OobeStage.Complete);
    }

    public override bool ValidateAndCommit()
    {
        if (!_found || string.IsNullOrEmpty(Session.InstallDir))
        {
            MessageBox.Show("尚未检测到 E听说 正在运行，请先启动该软件，或返回上一步改用手动方式。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }
}