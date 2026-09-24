using FuckETS.Services;

namespace FuckETS.Pages;

/// <summary>阶段 4：完成页，展示配置摘要并允许完成。</summary>
public partial class OobeCompletePage : OobePageBase
{
    public OobeCompletePage()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        CanProceed = true;

        if (Session.Mode == OobeStateService.AcquisitionMode.Manual)
        {
            ModeSummary.Text = "获取方式：手动输入";
        }
        else
        {
            ModeSummary.Text = "获取方式：自动获取";
        }

        DirSummary.Text = string.IsNullOrEmpty(Session.InstallDir)
            ? "E听说 安装目录：（未提供）"
            : $"E听说 安装目录：{Session.InstallDir}";
    }

    public override bool ValidateAndCommit() => true;
}