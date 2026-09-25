namespace FuckETS.PluginSdk.Abstractions;

/// <summary>
/// 插件类型。每个插件必须在清单中显式声明其类型（同一插件只能声明一种）。
/// </summary>
public enum PluginType
{
    /// <summary>行为插件：扩展软件行为（解析前后处理、结果加工、自定义导出、界面功能注入等）。</summary>
    Behavior = 0,

    /// <summary>解析插件：适配特定地区/实体的试题格式，替换或扩展默认解析与 Part 识别逻辑。</summary>
    Parser = 1,
}

/// <summary>
/// 插件 API 版本标识。主程序与插件据此做兼容性校验。
/// </summary>
public static class PluginApiVersion
{
    /// <summary>当前 SDK 版本。主程序只加载 API 版本兼容的插件。</summary>
    public const string Current = "1.0";
}