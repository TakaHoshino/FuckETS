using System.Windows;
using FuckETS.Services;

namespace FuckETS;

/// <summary>通用设置窗口（当前含检查更新设置）。</summary>
public partial class SettingsWindow : Window
{
    private bool _checking;

    public SettingsWindow()
    {
        InitializeComponent();
        var settings = UpdateSettingsService.Load();
        CheckOnStartupCheck.IsChecked = settings.CheckOnStartup;
        OfficialSourceRadio.IsChecked = settings.Source == UpdateSource.Official;
        ProxySourceRadio.IsChecked = settings.Source == UpdateSource.GhProxy;
    }

    /// <summary>把当前界面选择写入持久化（立即检查 / 确定时均调用，保证下载使用所选源）。</summary>
    private void PersistCurrentUi()
    {
        UpdateSettingsService.Save(new UpdateSettings
        {
            CheckOnStartup = CheckOnStartupCheck.IsChecked == true,
            Source = ProxySourceRadio.IsChecked == true ? UpdateSource.GhProxy : UpdateSource.Official,
        });
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        PersistCurrentUi();
        DialogResult = true;
    }

    private async void CheckNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checking)
            return;
        _checking = true;
        CheckNowButton.IsEnabled = false;
        CheckStatusText.Text = "检查中…";
        PersistCurrentUi();
        try
        {
            var result = await UpdateService.CheckAsync();
            if (!IsLoaded)
                return;

            if (result.UpdateAvailable)
            {
                CheckStatusText.Text = $"发现新版本 {result.LatestTag}";
                var prompt = new UpdatePromptWindow(result) { Owner = this };
                if (prompt.ShowDialog() == true)
                {
                    var win = new UpdateWindow(result.LatestTag, prompt.SelectedKind) { Owner = this };
                    win.ShowDialog();
                }
            }
            else
            {
                CheckStatusText.Text = $"已是最新版本（{UpdateService.GetCurrentTag()}）";
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"检查更新失败：{ex.Message}");
            if (IsLoaded)
            {
                CheckStatusText.Text = "检查失败（网络异常）";
                MessageBox.Show(this, $"检查更新失败：{ex.Message}", "检查更新",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            _checking = false;
            if (IsLoaded)
                CheckNowButton.IsEnabled = true;
        }
    }
}
