using System.Text.Json;
using FuckETS.PluginSdk.Abstractions;

namespace FuckETS.PluginSdk.Models;

/// <summary>清单校验结果。</summary>
public sealed record ManifestValidation(bool IsValid, string? Error);

/// <summary>插件清单校验工具。</summary>
public static class ManifestValidator
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>从 JSON 字符串解析清单；解析失败或字段非法返回 IsValid=false 与错误信息。</summary>
    public static ManifestValidation TryParse(string json, out PluginManifest? manifest)
    {
        manifest = null;
        if (string.IsNullOrWhiteSpace(json))
            return new ManifestValidation(false, "清单内容为空");

        // type 字段必须显式存在且为字符串（枚举默认值不能掩盖缺失）
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("type", out var typeProp)
                || typeProp.ValueKind != JsonValueKind.String)
            {
                return new ManifestValidation(false, "插件类型 type 缺失或非法（应为 behavior 或 parser）");
            }
        }
        catch (Exception ex)
        {
            return new ManifestValidation(false, $"plugin.json 解析失败：{ex.Message}");
        }

        try
        {
            var m = JsonSerializer.Deserialize<PluginManifest>(json, Options);
            if (m is null)
                return new ManifestValidation(false, "无法解析 plugin.json");
            manifest = m;
            return Validate(m);
        }
        catch (Exception ex)
        {
            return new ManifestValidation(false, $"plugin.json 解析失败：{ex.Message}");
        }
    }

    /// <summary>校验清单字段是否合法。</summary>
    public static ManifestValidation Validate(PluginManifest m)
    {
        if (string.IsNullOrWhiteSpace(m.Id))
            return new ManifestValidation(false, "缺少插件 id");
        if (string.IsNullOrWhiteSpace(m.Name))
            return new ManifestValidation(false, "缺少插件名称 name");
        if (m.Type is not (PluginType.Behavior or PluginType.Parser))
            return new ManifestValidation(false, "插件类型 type 缺失或非法（应为 behavior 或 parser）");
        if (string.IsNullOrWhiteSpace(m.Assembly))
            return new ManifestValidation(false, "缺少入口程序集 assembly");
        if (string.IsNullOrWhiteSpace(m.EntryType))
            return new ManifestValidation(false, "缺少入口类型 entryType");
        if (string.IsNullOrWhiteSpace(m.ApiVersion))
            return new ManifestValidation(false, "缺少 API 版本 apiVersion");

        // API 版本兼容校验
        var (apiOk, apiErr) = CheckApiVersion(m.ApiVersion);
        if (!apiOk)
            return new ManifestValidation(false, apiErr);

        return new ManifestValidation(true, null);
    }

    /// <summary>校验插件 API 版本与当前 SDK 是否兼容。</summary>
    private static (bool Ok, string? Error) CheckApiVersion(string pluginApi)
    {
        var cur = PluginApiVersion.Current; // 主版本.次版本
        var curMajor = Split(cur).Major;
        var pluginMajor = Split(pluginApi).Major;
        if (pluginMajor != curMajor)
            return (false, $"插件 API 版本 {pluginApi} 与主程序 {cur} 不兼容（主版本不一致）");
        return (true, null);

        static (int Major, int Minor) Split(string v)
        {
            var p = v.Split('.');
            int.TryParse(p.Length > 0 ? p[0] : "0", out var major);
            int.TryParse(p.Length > 1 ? p[1] : "0", out var minor);
            return (major, minor);
        }
    }
}