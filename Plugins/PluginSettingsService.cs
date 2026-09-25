using System.IO;
using System.Text.Json;

namespace FuckETS.Plugins;

/// <summary>插件配置持久化（启用/禁用、选中的解析插件）。</summary>
public static class PluginSettingsService
{
    private const string FileName = "plugins.json";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string GetSettingsPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "FuckETS", FileName);
    }

    public static PluginSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return new PluginSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginSettings>(json, Options) ?? new PluginSettings();
        }
        catch
        {
            return new PluginSettings();
        }
    }

    public static void Save(PluginSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(GetSettingsPath());
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(GetSettingsPath(), JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // 保存失败不致命
        }
    }
}

/// <summary>插件配置模型。</summary>
public sealed class PluginSettings
{
    /// <summary>插件是否禁用（key = 插件 id）。</summary>
    public Dictionary<string, bool> Enabled { get; set; } = new();

    /// <summary>当前选中的解析插件 id（主界面下拉框）。</summary>
    public string? SelectedParserId { get; set; }
}