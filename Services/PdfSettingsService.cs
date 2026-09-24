using System.IO;
using System.Text.Json;
using FuckETS.Models;

namespace FuckETS.Services;

/// <summary>PDF 导出设置的 JSON 持久化。</summary>
public static class PdfSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>获取设置文件路径（%APPDATA%\FuckETS\settings.json）。</summary>
    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FuckETS", "settings.json");
    }

    /// <summary>加载设置；文件缺失或损坏时返回默认值。</summary>
    public static PdfExportOptions Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return PdfExportOptions.Default();

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<PdfExportOptions>(json, SerializerOptions);
            return Sanitize(loaded) ?? PdfExportOptions.Default();
        }
        catch
        {
            return PdfExportOptions.Default();
        }
    }

    /// <summary>保存设置。</summary>
    public static void Save(PdfExportOptions options)
    {
        try
        {
            var dir = Path.GetDirectoryName(GetSettingsPath());
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Sanitize(options) ?? PdfExportOptions.Default(), SerializerOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch
        {
            // 保存失败不致命，忽略即可
        }
    }

    /// <summary>限制参数在合理范围内，避免非法值导致 PDF 生成异常。</summary>
    private static PdfExportOptions? Sanitize(PdfExportOptions? o)
    {
        if (o is null)
            return null;

        o.FontSize = Clamped(o.FontSize, 8f, 72f, 20f);
        o.TitleFontSize = Clamped(o.TitleFontSize, 10f, 96f, 24f);
        o.PartHeadingFontSize = Clamped(o.PartHeadingFontSize, 10f, 72f, 22f);
        o.MarginMm = Clamped(o.MarginMm, 5f, 50f, 20f);
        o.LineHeight = Clamped(o.LineHeight, 1f, 3f, 1.2f);
        return o;
    }

    private static float Clamped(float value, float min, float max, float fallback)
    {
        return float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }
}