using System.Windows;
using FuckETS.Services;

namespace FuckETS;

/// <summary>更新询问弹窗：说明新版本，并让用户选择自包含版（sc）/ 框架依赖版（fd）后确认是否更新。</summary>
public partial class UpdatePromptWindow : Window
{
    /// <summary>用户选择的安装包类型。</summary>
    public UpdateAssetKind SelectedKind { get; private set; } = UpdateAssetKind.SelfContained;

    public UpdatePromptWindow(UpdateCheckResult result)
    {
        InitializeComponent();

        SummaryText.Text = $"发现新版本 {result.LatestTag}（当前 {UpdateService.GetCurrentTag()}），是否更新？";
        ScSizeText.Text = FormatSize(result.SelfContainedSizeBytes);
        FdSizeText.Text = FormatSize(result.FrameworkDependentSizeBytes);

        // 默认选中与当前安装一致的类型
        if (UpdateService.IsSelfContained())
        {
            SelfContainedRadio.IsChecked = true;
            InstallTypeHint.Text = "当前为自包含安装，推荐保持「自包含版」。";
        }
        else
        {
            FrameworkRadio.IsChecked = true;
            InstallTypeHint.Text = "当前为框架依赖安装，若目标机器未装 .NET 8 桌面运行时请改选「自包含版」。";
        }
    }

    private static string FormatSize(long? bytes)
        => bytes is > 0 ? $"（约 {bytes.Value / 1048576.0:F0} MB）" : string.Empty;

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedKind = SelfContainedRadio.IsChecked == true
            ? UpdateAssetKind.SelfContained
            : UpdateAssetKind.FrameworkDependent;
        DialogResult = true;
    }
}
