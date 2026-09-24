using System.Windows;
using System.Windows.Threading;
using FuckETS.Services;

namespace FuckETS.Pages;

/// <summary>阶段 1：免责声明同意（含 5 秒倒计时）。</summary>
public partial class OobeDisclaimerPage : OobePageBase
{
    private const int CountdownSeconds = 5;
    private readonly DispatcherTimer _countdownTimer;
    private int _remaining;
    private bool _agreed;

    public OobeDisclaimerPage()
    {
        InitializeComponent();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    protected override void OnInitialized()
    {
        // 若已同意过（续做），直接允许继续
        _agreed = Session.DisclaimerAccepted;
        if (_agreed)
        {
            Logger.Debug("免责声明：检测到已临时同意（续做），直接允许进入下一步。");
            AgreeButton.IsEnabled = true;
            AgreeButton.Content = "同意";
            CanProceed = true;
            return;
        }

        // 开始倒计时
        Logger.Debug("免责声明：开始 5 秒倒计时，等待用户阅读并同意。");
        _remaining = CountdownSeconds;
        CanProceed = false;
        AgreeButton.IsEnabled = false;
        AgreeButton.Content = $"同意（{_remaining} 秒）";
        _countdownTimer.Start();
    }

    public override void OnPageClosed()
    {
        _countdownTimer.Stop();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _countdownTimer.Stop();
            AgreeButton.IsEnabled = true;
            AgreeButton.Content = "同意";
        }
        else
        {
            AgreeButton.Content = $"同意（{_remaining} 秒）";
        }
    }

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        _agreed = true;
        Session.DisclaimerAccepted = true;
        OobeStateService.SaveDisclaimerAccepted();
        OobeStateService.SaveStage(OobeStateService.OobeStage.InstallSource);
        CanProceed = true;
        AgreeButton.IsEnabled = false; // 已同意，不可撤销
        Logger.Info("用户已同意免责声明。");
    }

    public override bool ValidateAndCommit()
    {
        if (!_agreed)
        {
            MessageBox.Show("请先阅读并同意免责声明。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }
}