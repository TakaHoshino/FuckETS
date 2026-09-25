using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Models;

namespace DemoParserPlugin;

/// <summary>
/// 示例解析插件：演示 <see cref="IParserPlugin"/> 的三个方法约定。
/// 行为：把全部 content_* 目录都识别为 PartA（PartB/PartC 将输出「未找到对应的子文件夹」），
/// 解析输出为占位说明行——用于在主界面下拉框切换插件后直观看到解析器已改变。
/// </summary>
public sealed class DemoParser : IParserPlugin
{
    private const string PartA = "PartA";
    private const string PartB = "PartB";
    private const string PartC = "PartC";

    public string RegionId => "Demo-Region";
    public string? EntityId => null;

    public void OnLoad() { }

    public PluginManifest GetManifest() => new()
    {
        Id = "example.demo-parser",
        Name = "示例·解析插件",
        Version = "1.0.0",
        Author = "FuckETS 示例",
        Type = PluginType.Parser,
        Region = RegionId,
        Assembly = "DemoParserPlugin.dll",
        EntryType = typeof(DemoParser).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "演示解析插件：全部 content 目录识别为 PartA 并输出占位内容。",
    };

    public SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context)
    {
        var result = new SortedDictionary<string, List<SubItem>>(StringComparer.Ordinal)
        {
            [PartA] = new(), [PartB] = new(), [PartC] = new(),
        };
        // 演示：把所有 content 目录都归入 PartA（真实插件应按 format 特征分组）
        result[PartA].AddRange(context.ContentItems);
        return result;
    }

    public string GetPartLabel(string part) => part switch
    {
        PartA => "示例·朗读",
        PartB => "示例·角色扮演",
        PartC => "示例·复述",
        _ => part,
    };

    public IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath)
    {
        var dirName = Path.GetFileName(Path.GetDirectoryName(contentJsonPath) ?? string.Empty);
        return
        [
            $"（DemoParser 示例解析插件，region={RegionId}）",
            $"Part={part}，来源目录={dirName}，content 目录总数={context.ContentItems.Count}。",
            "这是示例占位内容。请改写 DemoParser 以接入真实试题格式。",
        ];
    }
}