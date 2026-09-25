using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Models;

namespace FuckETS.PluginSdk.Models;

/// <summary>已加载插件的运行时信息（由主程序宿主维护，供插件管理器与 UI 使用）。</summary>
public sealed class PluginInfo
{
    /// <summary>残元数据（来自插件对象的 GetManifest()，通常与包清单一致）。</summary>
    public PluginManifest Manifest { get; set; } = new();

    /// <summary>插件实例（解析插件或行为插件）。</summary>
    public IPlugin? Instance { get; set; }

    /// <summary>装载来源：内置（BuiltIn）或 .fep 文件路径。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>是否为内置插件（随程序分发、不可删除）。</summary>
    public bool BuiltIn { get; set; }

    /// <summary>是否启用（可在配置中禁用）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>是否类型为解析插件。</summary>
    public bool IsParser => Manifest.Type == PluginType.Parser;

    /// <summary>是否类型为行为插件。</summary>
    public bool IsBehavior => Manifest.Type == PluginType.Behavior;

    /// <summary>解析插件的地区/实体标识。</summary>
    public string RegionId => (Instance as IParserPlugin)?.RegionId ?? Manifest.Region;

    /// <summary>可读名称（含版本、类型、地区）。</summary>
    public string DisplayName =>
        $"{Manifest.Name} {Manifest.Version} " +
        $"({(IsParser ? "解析" : "行为")}" +
        $"{(IsParser && !string.IsNullOrEmpty(RegionId) ? $"/ {RegionId}" : "")})";

    /// <summary>来源的简短标签（内置 / 插件文件名）。</summary>
    public string SourceLabel => BuiltIn ? "内置" : System.IO.Path.GetFileName(Source);

    /// <summary>启用状态标签。</summary>
    public string StateLabel => Enabled ? "已启用" : "已禁用";
}