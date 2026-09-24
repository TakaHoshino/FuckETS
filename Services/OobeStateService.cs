using System.IO;
using Microsoft.Win32;

namespace FuckETS.Services;

/// <summary>OOBE（出厂初始设置向导）状态的服务，负责注册表持久化与校验。</summary>
public static class OobeStateService
{
    private const string RegistryKeyPath = @"Software\FuckETS";

    /// <summary>安装目录获取方式。</summary>
    public enum AcquisitionMode
    {
        NotSet,
        Auto,
        Manual,
    }

    /// <summary>OOBE 阶段（用于阶段持久化与重启后从已完成阶段继续）。</summary>
    public enum OobeStage
    {
        Disclaimer = 1,
        InstallSource = 2,
        LaunchEts = 3,
        Complete = 4,
    }

    /// <summary>判断 OOBE 是否已完整完成且校验通过。</summary>
    public static bool IsCompleted()
    {
        try
        {
            using var key = OpenKey(false);
            if (key is null)
                return false;

            // 1. 完成标志必须为有效的 DWORD 1
            var completed = key.GetValue("Completed");
            if (!IsValidEnabledFlag(completed))
                return false;

            // 2. 免责声明同意标志必须有效
            var disclaimer = key.GetValue("DisclaimerAccepted");
            if (!IsValidEnabledFlag(disclaimer))
                return false;

            // 3. 安装目录必须存在且为非空字符串
            var installDir = key.GetValue("EtsInstallDir") as string;
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
                return false;

            // 4. 获取方式有效
            var mode = GetAcquisitionMode(key.GetValue("AcquisitionMode")?.ToString());
            if (mode == AcquisitionMode.NotSet)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            // 注册表读取失败视为未完成
            Logger.Error("OOBE 状态校验异常。", ex);
            return false;
        }
    }

    /// <summary>获取上次完成的 OOBE 阶段（用于重启后继续）。完成时返回 Complete。</summary>
    public static OobeStage GetCurrentStage()
    {
        try
        {
            using var key = OpenKey(false);
            if (key is null)
                return OobeStage.Disclaimer;

            // 已完整完成则直接返回完成
            if (IsCompleted())
                return OobeStage.Complete;

            var stageVal = key.GetValue("Stage") as int? ?? 0;
            if (stageVal is >= 1 and <= 4)
                return (OobeStage)stageVal;
            return OobeStage.Disclaimer;
        }
        catch
        {
            return OobeStage.Disclaimer;
        }
    }

    /// <summary>记录已完成的阶段（用于重启续做）。</summary>
    public static void SaveStage(OobeStage stage)
    {
        try
        {
            using var key = OpenKey(true);
            key?.SetValue("Stage", (int)stage, RegistryValueKind.DWord);
            Logger.Debug($"已将 OOBE 阶段写为 {stage}。");
        }
        catch (Exception ex)
        {
            Logger.Error($"写入 OOBE 阶段 {stage} 失败。", ex);
        }
    }

    /// <summary>记录免责声明已同意（含时间戳）。</summary>
    public static void SaveDisclaimerAccepted()
    {
        try
        {
            using var key = OpenKey(true);
            key?.SetValue("DisclaimerAccepted", 1, RegistryValueKind.DWord);
            key?.SetValue("DisclaimerAcceptedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
            Logger.Info("免责声明已同意并记录。");
        }
        catch (Exception ex)
        {
            Logger.Error("记录免责声明同意状态失败。", ex);
        }
    }

    /// <summary>保存安装目录与获取方式（阶段 4 完成前持久化，便于续做）。</summary>
    public static void SaveInstallInfo(string installDir, AcquisitionMode mode)
    {
        try
        {
            using var key = OpenKey(true);
            if (!string.IsNullOrEmpty(installDir))
                key?.SetValue("EtsInstallDir", installDir, RegistryValueKind.String);
            key?.SetValue("AcquisitionMode", ModeToString(mode), RegistryValueKind.String);
            Logger.Debug($"已保存安装信息：方式={mode}，目录={installDir}。");
        }
        catch (Exception ex)
        {
            Logger.Error("保存安装信息失败。", ex);
        }
    }

    /// <summary>完成 OOBE：写入完整状态。返回是否成功。</summary>
    public static bool CompleteOobe(string installDir, AcquisitionMode mode)
    {
        try
        {
            using var key = OpenKey(true);
            if (key is null)
            {
                Logger.Error("无法打开注册表键以完成 OOBE。");
                return false;
            }
            key.SetValue("Completed", 1, RegistryValueKind.DWord);
            key.SetValue("Stage", (int)OobeStage.Complete, RegistryValueKind.DWord);
            key.SetValue("DisclaimerAccepted", 1, RegistryValueKind.DWord);
            key.SetValue("DisclaimerAcceptedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
            key.SetValue("EtsInstallDir", installDir, RegistryValueKind.String);
            key.SetValue("AcquisitionMode", ModeToString(mode), RegistryValueKind.String);
            Logger.Info($"OOBE 完成记录已写入注册表（方式={mode}，目录={installDir}）。");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("写入 OOBE 完成状态失败。", ex);
            return false;
        }
    }

    /// <summary>读取已保存的安装目录（可能为空）。</summary>
    public static string? GetInstallDir()
    {
        try
        {
            using var key = OpenKey(false);
            return key?.GetValue("EtsInstallDir") as string;
        }
        catch (Exception ex)
        {
            Logger.Error("读取已保存的安装目录失败。", ex);
            return null;
        }
    }

    private static string ModeToString(AcquisitionMode mode) => mode switch
    {
        AcquisitionMode.Auto => "auto",
        AcquisitionMode.Manual => "manual",
        _ => "notset",
    };

    private static AcquisitionMode GetAcquisitionMode(string? value)
    {
        return value switch
        {
            string v when v.Equals("auto", StringComparison.OrdinalIgnoreCase) => AcquisitionMode.Auto,
            string v when v.Equals("manual", StringComparison.OrdinalIgnoreCase) => AcquisitionMode.Manual,
            _ => AcquisitionMode.NotSet,
        };
    }

    /// <summary>校验注册表值是否为有效的“启用”标志（DWORD 1 或字符串 "1"）。</summary>
    private static bool IsValidEnabledFlag(object? value)
    {
        return value switch
        {
            int i when i == 1 => true,
            string s when s.Trim() == "1" => true,
            _ => false,
        };
    }

    /// <summary>打开（必要时创建）注册表键。创建失败或权限不足时返回 null。</summary>
    private static RegistryKey? OpenKey(bool write)
    {
        try
        {
            // 注意：不释放 Registry.CurrentUser 根键（系统全局句柄）。
            return write
                ? Registry.CurrentUser.CreateSubKey(RegistryKeyPath, writable: true)
                : Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
        }
        catch
        {
            return null;
        }
    }
}