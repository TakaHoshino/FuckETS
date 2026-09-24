using System.Diagnostics;
using System.IO;

namespace FuckETS.Services;

/// <summary>E听说 进程检测与安装根目录推导。</summary>
public static class EtsInstallService
{
    /// <summary>典型 E听说 可执行文件名（不含扩展名，忽略大小写比较）。</summary>
    private static readonly string[] TypicalEtsExeNames =
    {
        "ets", "etsclient", "e听说", "e-like", "elistening",
    };

    private static readonly string[] TypicalRootMarkers =
    {
        "e听说", "elistening", ".ets",
    };

    /// <summary>当前应用自身的进程名（用于排除，避免把本软件误判为 E听说）。</summary>
    private static string? _ownProcessName;
    private static string? _ownExePath;

    private static bool IsOwnProcess(Process process)
    {
        _ownProcessName ??= Process.GetCurrentProcess().ProcessName;
        _ownExePath ??= Process.GetCurrentProcess().MainModule?.FileName;
        try
        {
            if (string.Equals(process.ProcessName, _ownProcessName, StringComparison.OrdinalIgnoreCase))
                return true;
            var exe = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe) &&
                !string.IsNullOrEmpty(_ownExePath) &&
                string.Equals(Path.GetFullPath(exe), Path.GetFullPath(_ownExePath), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // 忽略
        }
        return false;
    }

    /// <summary>
    /// 判断进程名是否可能是 E听说（排除常见系统/其它软件及本软件自身的误伤）。
    /// </summary>
    private static bool IsLikelyEtsProcessName(string fileNameWithoutExt)
    {
        var lower = fileNameWithoutExt.ToLowerInvariant();

        // 明确误伤名单：名字里虽含 "ets" 但绝不属于 E听说 的系统/其它组件
        var falsePositives = new[]
        {
            "widget", "getstarted", "onegetsets", "settings",
            "predict", "assets", "package",
        };
        if (falsePositives.Any(fp => lower.Contains(fp)))
            return false;

        // 1. 精确/常见 E听说 可执行名
        if (TypicalEtsExeNames.Any(n => lower.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                                       lower.StartsWith(n, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 2. 宽松回退：名字含 "ets" 或 "e听说"（此时驱动特征校验兜底，避免把 FuckETS 之类误判）
        return lower.Contains("ets") || lower.Contains("e听说");
    }

    /// <summary>
    /// 校验某个目录是否具备 E听说 安装目录的特征：
    /// 该目录本身、其父目录或直接子目录下存在 logs\ets_*.log，或包含典型 E听说 可执行文件。
    /// </summary>
    private static bool HasEtsInstallFeatures(string dir)
    {
        // 组装候选路径（目录本身、父目录、直接子目录）
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        candidates.Add(dir);
        var parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrEmpty(parent))
            candidates.Add(parent);
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir))
                candidates.Add(sub);
        }
        catch
        {
            // 忽略无权限子目录
        }

        foreach (var cand in candidates)
        {
            // 特征1：存在 logs\ets_*.log
            var logsDir = Path.Combine(cand, "logs");
            try
            {
                if (Directory.Exists(logsDir) && Directory.EnumerateFiles(logsDir, "ets_*.log").Any())
                    return true;
            }
            catch
            {
                // 忽略
            }

            // 特征2：目录名暗示 E听说
            var candName = Path.GetFileName(cand) ?? "";
            if (TypicalRootMarkers.Any(m => candName.Contains(m, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    /// <summary>检测 E听说 相关进程是否正在运行（进程名匹配且目录具特征，或目录具强特征）。</summary>
    public static bool IsEtsRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (IsOwnProcess(process))
                    continue;
                var exe = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(exe))
                    continue;
                var name = Path.GetFileNameWithoutExtension(exe);
                if (!IsLikelyEtsProcessName(name))
                    continue;
                var exeDir = Path.GetDirectoryName(Path.GetFullPath(exe));
                if (string.IsNullOrEmpty(exeDir))
                    continue;
                // 进程名匹配即认为在运行（即便尚未确认目录特征）
                return true;
            }
            catch
            {
                // 忽略无权限进程
            }
        }
        return false;
    }

    /// <summary>
    /// 从正在运行的 ETS 进程推导安装根目录（可执行文件所在目录或其父目录）。
    /// 仅返回具备 E听说 目录特征的结果；未发现则返回 null。
    /// </summary>
    public static string? DetachInstallRootFromProcess()
    {
        // 收集所有匹配进程的可执行文件目录
        var candidates = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (IsOwnProcess(process))
                    continue;
                var exe = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(exe))
                    continue;
                if (!IsLikelyEtsProcessName(Path.GetFileNameWithoutExtension(exe)))
                    continue;
                var exeDir = Path.GetDirectoryName(Path.GetFullPath(exe));
                if (string.IsNullOrEmpty(exeDir))
                    continue;
                candidates.Add(exeDir);
            }
            catch
            {
                // 忽略无权限进程，继续下一个
            }
        }

        // 已利用 HashSet 去重，避免同一目录被重复处理
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exeDir in candidates)
        {
            if (!seen.Add(exeDir))
                continue;

            var root = ChooseRoot(exeDir);
            if (root is null)
                continue;

            // 只返回具备 E听说 安装目录特征的路径，避免误判为系统组件等。
            if (HasEtsInstallFeatures(root) || HasEtsInstallFeatures(exeDir))
            {
                Logger.Debug($"自动检测到 E听说 安装根目录：{root}（由进程目录 {exeDir} 推导）。");
                return root;
            }
        }

        // 回退：未从运行中的进程定位到 E听说，则扫描各盘根目录下的 ETS 目录（含 logs\ets_*.log 者）。
        var rootFromDrives = FindInstallRootFromDriveRoots();
        if (rootFromDrives is not null)
        {
            Logger.Debug($"自动检测到 E听说 安装根目录：{rootFromDrives}（由盘符根目录 ETS 目录匹配）。");
            return rootFromDrives;
        }

        return null;
    }

    /// <summary>在各盘符根目录下查找含 logs\ets_*.log 的 ETS 目录，作为 E听说 安装根目录的回退来源。</summary>
    private static string? FindInstallRootFromDriveRoots()
    {
        for (char letter = 'A'; letter <= 'Z'; letter++)
        {
            var etsDir = $"{letter}:\\ETS";
            try
            {
                if (!Directory.Exists(etsDir))
                    continue;
                var logsDir = Path.Combine(etsDir, "logs");
                if (Directory.Exists(logsDir) && Directory.EnumerateFiles(logsDir, "ets_*.log").Any())
                    return etsDir;
            }
            catch
            {
                // 忽略无法访问的盘符
            }
        }
        return null;
    }

    /// <summary>取最可能是安装根目录的路径：目录本身或其父目录（一般 ETS 可执行文件位于子目录或根目录）。</summary>
    private static string? ChooseRoot(string exeDir)
    {
        if (Directory.Exists(exeDir))
            return exeDir;

        var parent = Path.GetDirectoryName(exeDir);
        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            return parent;

        return exeDir;
    }

    /// <summary>校验给定路径是否为有效目录（用于手动输入的二次校验）。</summary>
    public static bool IsValidDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }
}