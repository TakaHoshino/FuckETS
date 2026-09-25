using System.IO;
using System.Windows;
using FuckETS.Services;

namespace FuckETS;

/// <summary>更新下载进度窗口：下载完成后应用更新并重启程序。</summary>
public partial class UpdateWindow : Window
{
    private readonly string _latestTag;
    private readonly UpdateAssetKind _assetKind;
    private readonly CancellationTokenSource _cts = new();
    private bool _applying;
    private bool _done;

    public UpdateWindow(string latestTag, UpdateAssetKind assetKind)
    {
        InitializeComponent();
        _latestTag = latestTag;
        _assetKind = assetKind;

        Closing += (_, e) =>
        {
            if (_done)
                return;
            if (_applying)
            {
                // 更新应用中不允许关闭窗口
                e.Cancel = true;
                return;
            }
            _cts.Cancel();
        };

        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var settings = UpdateSettingsService.Load();
        try
        {
            var uri = UpdateService.GetDownloadUri(_latestTag, settings.Source, _assetKind);
            Logger.Info($"开始下载更新 {_latestTag}：{uri}");
            StatusText.Text = $"正在下载 {_latestTag}（{Path.GetFileName(uri.LocalPath)}）…";

            var progress = new Progress<(long Bytes, long Total)>(p =>
            {
                if (p.Total > 0)
                {
                    DownloadBar.IsIndeterminate = false;
                    DownloadBar.Maximum = p.Total;
                    DownloadBar.Value = p.Bytes;
                    ProgressText.Text = $"{p.Bytes / 1048576.0:F1} / {p.Total / 1048576.0:F1} MB";
                }
                else
                {
                    ProgressText.Text = $"{p.Bytes / 1048576.0:F1} MB";
                }
            });

            var zip = await UpdateService.DownloadAsync(uri, progress, _cts.Token);

            _applying = true;
            CancelButton.IsEnabled = false;
            StatusText.Text = "下载完成，正在应用更新…";
            DownloadBar.IsIndeterminate = true;
            ProgressText.Text = string.Empty;

            UpdateService.Apply(zip);

            StatusText.Text = "更新完成，程序将自动重启…";
            await Task.Delay(1000);
            _done = true;
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            if (_cts.IsCancellationRequested)
            {
                Logger.Info("用户取消更新。");
                _done = true;
                Close();
            }
            else
            {
                // HttpClient 超时（非用户取消）
                Logger.Error("更新下载超时。");
                _done = true;
                DownloadBar.IsIndeterminate = false;
                MessageBox.Show(this, "下载超时。可尝试更换下载源（gh-proxy 代理源）后重试。", "检查更新",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("更新失败。", ex);
            _done = true;
            DownloadBar.IsIndeterminate = false;
            MessageBox.Show(this, $"更新失败：{ex.Message}", "检查更新", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_applying)
            return;
        _cts.Cancel();
    }
}
