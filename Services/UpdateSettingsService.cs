using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuckETS.Services;

/// <summary>更新下载源。</summary>
public enum UpdateSource
{
    /// <summary>官方源（github.com 直连）。</summary>
    Official,

    /// <summary>gh-proxy 代理源（国内加速）。</summary>
    GhProxy,
}

/// <summary>检查更新设置。</summary>
public sealed class UpdateSettings
{
    /// <summary>每次启动时是否自动检查更新。</summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>更新下载源。</summary>
    public UpdateSource Source { get; set; } = UpdateSource.Official;
}

/// <summary>检查更新设置的 JSON 持久化（%APPDATA%\FuckETS\update.json）。</summary>
public static class UpdateSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>获取设置文件路径。</summary>
    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "FuckETS", "update.json");
    }

    /// <summary>加载设置；文件缺失或损坏时返回默认值。</summary>
    public static UpdateSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return new UpdateSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UpdateSettings>(json, SerializerOptions) ?? new UpdateSettings();
        }
        catch
        {
            return new UpdateSettings();
        }
    }

    /// <summary>保存设置；失败不致命。</summary>
    public static void Save(UpdateSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(GetSettingsPath());
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(GetSettingsPath(), JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch
        {
            // 保存失败不致命，忽略即可
        }
    }
}
