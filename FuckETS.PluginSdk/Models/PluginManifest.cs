using System.Text.Json.Serialization;
using FuckETS.PluginSdk.Abstractions;

namespace FuckETS.PluginSdk.Models;

/// <summary>
/// 插件清单（plugin.json）。插件 .fep 包内必须包含该清单。
/// </summary>
public sealed class PluginManifest
{
    /// <summary>唯一标识（建议反向域名，如 com.example.myplugin）。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>版本（语义化版本，如 1.0.0）。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>作者。</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>插件类型：behavior 或 parser。缺失或非法则拒绝加载。</summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginType Type { get; set; }

    /// <summary>地区/实体标识（仅解析插件有意义），如 "Guangdong-HighSchool"。</summary>
    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    /// <summary>入口程序集文件名（.fep 包内，不含扩展名上的依赖，如 "MyPlugin.dll"）。</summary>
    [JsonPropertyName("assembly")]
    public string Assembly { get; set; } = string.Empty;

    /// <summary>定位入口类型的完全限定名（如 "MyPlugin.ParserPlugin"）。</summary>
    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = string.Empty;

    /// <summary>插件要求的主程序 API 版本。不兼容则拒绝加载。</summary>
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "1.0";

    /// <summary>是否内置插件（随程序分发、不可删除）。主程序扩展元数据使用。</summary>
    [JsonPropertyName("builtIn")]
    public bool BuiltIn { get; set; }

    /// <summary>描述。</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}