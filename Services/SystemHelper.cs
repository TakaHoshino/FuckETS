using System.IO;

namespace FuckETS.Services;

/// <summary>系统相关工具：中文字体查找与目录创建时间。</summary>
public static class SystemHelper
{
    /// <summary>在系统字体目录中查找指定名称的字体文件；找不到返回 null。</summary>
    public static string? FindSystemFont(string fileName)
    {
        var fontDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        var path = Path.Combine(fontDir, fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>查找可用的中文字体文件，按优先级返回第一个存在的路径。</summary>
    public static string? FindChineseFont()
    {
        var fontDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Fonts",
            "/usr/share/fonts/truetype/droid",
            "/System/Library/Fonts",
        };

        // 优先选择 .ttf 单文件字体以提高 PDF 嵌入兼容性（QuestPDF/TTC 支持有限）
        var candidates = new[]
        {
            "simhei.ttf",
            "msyh.ttf",
            "simsun.ttc",
            "msyh.ttc",
            "syf_ban.ttf",
            "DroidSansFallbackFull.ttf",
            "PingFang.ttc",
        };

        foreach (var dir in fontDirs)
        {
            foreach (var name in candidates)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return path;
            }
        }

        // 兜底：在字体目录中任选一个 .ttc/.ttf
        if (Directory.Exists(fontDirs[0]))
        {
            foreach (var file in Directory.EnumerateFiles(fontDirs[0]))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".ttc" || ext == ".ttf")
                    return file;
            }
        }

        return null;
    }

    /// <summary>获取目录创建时间字符串，格式 yyyy-MM-dd HH:mm:ss。</summary>
    public static string GetCreationTime(string path) => GetCreationTime(new DirectoryInfo(path));

    /// <summary>获取目录创建时间字符串，格式 yyyy-MM-dd HH:mm:ss。</summary>
    public static string GetCreationTime(DirectoryInfo dir)
    {
        try
        {
            return dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "无法获取时间";
        }
    }
}