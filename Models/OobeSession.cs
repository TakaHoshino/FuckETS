using FuckETS.Services;

namespace FuckETS.Models;

/// <summary>OOBE 会话中的共享进度状态（供各阶段页面读写）。</summary>
public sealed class OobeSession
{
    /// <summary>免责声明是否已同意。</summary>
    public bool DisclaimerAccepted { get; set; }

    /// <summary>获取方式：自动 / 手动/未选择。</summary>
    public OobeStateService.AcquisitionMode Mode { get; set; } = OobeStateService.AcquisitionMode.NotSet;

    /// <summary>E听说 安装目录（自动或手动获取）。</summary>
    public string InstallDir { get; set; } = string.Empty;

    /// <summary>是否在启动时续做（重启后从已完成阶段继续）。</summary>
    public bool Resuming { get; set; }
}