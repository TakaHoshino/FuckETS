using System.IO;

namespace FuckETS.Services;

/// <summary>扫描 %APPDATA%\ETS\ 下纯数字命名的子文件夹。</summary>
public static class EtsScanner
{
    /// <summary>定位 ETS 基础目录，返回 null 表示未找到 APPDATA 或目录不存在。</summary>
    public static string? GetBaseDirectory(out string? error)
    {
        var appdata = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrEmpty(appdata))
        {
            error = "未找到 APPDATA 环境变量";
            Logger.Error("未找到 APPDATA 环境变量。");
            return null;
        }

        var baseDir = Path.Combine(appdata, "ETS");
        if (!Directory.Exists(baseDir))
        {
            error = $"目录 {baseDir} 不存在";
            Logger.Warn($"ETS 目录不存在：{baseDir}");
            return null;
        }

        error = null;
        return baseDir;
    }

    /// <summary>扫描基础目录下的纯数字子文件夹。</summary>
    public static List<Models.FolderItem> Scan(string baseDir)
    {
        var folders = new List<Models.FolderItem>();
        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(baseDir))
            {
                var dir = new DirectoryInfo(dirPath);
                if (IsAllDigits(dir.Name))
                {
                    folders.Add(new Models.FolderItem(
                        dir.Name,
                        dir.FullName,
                        SystemHelper.GetCreationTime(dir)));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"扫描目录 {baseDir} 出错：{ex.Message}", ex);
        }
        return folders;
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        foreach (var c in s)
            if (!char.IsDigit(c))
                return false;
        return true;
    }
}