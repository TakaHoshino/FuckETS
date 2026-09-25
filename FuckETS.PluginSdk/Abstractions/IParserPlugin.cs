using FuckETS.PluginSdk.Context;

namespace FuckETS.PluginSdk.Abstractions;

/// <summary>
/// 解析插件接口：适配特定地区/实体的试题格式，替换或扩展默认解析与 Part 识别逻辑。
/// </summary>
public interface IParserPlugin : IPlugin
{
    /// <summary>声明插件支持的地区/实体标识（如 "Guangdong-HighSchool"）。用于解析插件匹配。</summary>
    string RegionId { get; }

    /// <summary>
    /// 识别每个 content_* 子目录属于哪个 Part。
    /// 返回「Part 键 → 该 Part 命中的所有 content 项（按 Id 升序）」。无法识别的 content 项不放回任何键。
    /// 主程序会据此为同一 Part 取 Id 最大（最新）的目录，并围绕此方法投递 Part 识别前后钩子。
    /// </summary>
    SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context);

    /// <summary>返回某个 Part 键的人类可读标签（如 "模仿朗读"）。未知名键可返回原键。</summary>
    string GetPartLabel(string part);

    /// <summary>解析某个 Part 对应 content.json 的正文，返回最终输出行（不含【Part】标题，标题由主程序拼接）。</summary>
    IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath);
}