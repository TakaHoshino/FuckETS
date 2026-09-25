using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Models;

namespace FuckETS.Plugins;

/// <summary>
/// 内置「广东高中」解析插件。把默认的 content.json 解析与 Part 识别（structure_type + 特征评分）封装为插件。
/// 完全基于 PluginSdk 契约实现，不依赖主程序内部类型，可作为自定义解析插件的参考骨架。
/// </summary>
public sealed class BuiltInGuangdongPlugin : IParserPlugin
{
    private const string Region = "Guangdong-HighSchool";

    // Part 键
    private const string PartA = "PartA";
    private const string PartB = "PartB";
    private const string PartC = "PartC";

    private static readonly Dictionary<string, string> StructureTypeMap = new()
    {
        ["collector.read"] = PartA,
        ["collector.3q5a"] = PartB,
        ["collector.picture"] = PartC,
    };

    private static readonly Regex ReVideoTag = new(@"\.mp4$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RePartBQuesAudio = new(@"^ques\d+.*\.mp3$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RePartCQuesAudio = new(@"^quesStd\d+\.mp3$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string RegionId => Region;
    public string? EntityId => null;

    public void OnLoad() { }

    public PluginManifest GetManifest() => new()
    {
        Id = PluginHost.BuiltInGuangdongId,
        Name = "广东高中试题解析",
        Version = "2.0.1",
        Author = "FuckETS",
        Type = PluginType.Parser,
        Region = Region,
        Assembly = "FuckETS.dll",
        EntryType = typeof(BuiltInGuangdongPlugin).FullName ?? "",
        ApiVersion = PluginApiVersion.Current,
        BuiltIn = true,
        Description = "解析广东地区高中 E听说 试题。",
    };

    public SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context)
    {
        var result = new SortedDictionary<string, List<SubItem>>(StringComparer.Ordinal)
        {
            [PartA] = new(), [PartB] = new(), [PartC] = new(),
        };

        foreach (var item in context.ContentItems)
        {
            var part = Detect(item.Path);
            if (part is not null && result.ContainsKey(part))
                result[part].Add(item);
        }
        return result;
    }

    public string GetPartLabel(string part) => part switch
    {
        PartA => "模仿朗读",
        PartB => "角色扮演",
        PartC => "故事复述",
        _ => part,
    };

    public IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath)
    {
        var lines = new List<string>();
        if (!File.Exists(contentJsonPath))
        {
            lines.Add($"错误：未找到 {contentJsonPath}");
            return lines;
        }

        try
        {
            var data = LoadJson(contentJsonPath);
            var info = data?.Info;
            switch (part)
            {
                case PartA:
                case PartC:
                    lines.Add(StripHtmlTags(FormatTextWithParagraphs(info?.Value)).Trim());
                    break;
                case PartB:
                    ParseQuestions(info?.Question, lines);
                    break;
            }
        }
        catch (Exception ex)
        {
            lines.Add($"[{part} 解析错误] {ex.Message}");
        }

        return lines;
    }

    // ---- 解析实现 ----

    private static string? Detect(string subdir)
    {
        var byType = DetectByStructureType(subdir);
        if (byType is not null) return byType;
        return DetectByFeatures(subdir);
    }

    private static string? DetectByStructureType(string subdir)
    {
        var jsonPath = Path.Combine(subdir, "content.json");
        if (!File.Exists(jsonPath)) return null;
        var data = LoadJson(jsonPath);
        var st = data?.StructureType;
        return st is not null && StructureTypeMap.TryGetValue(st, out var part) ? part : null;
    }

    private static string? DetectByFeatures(string subdir)
    {
        var scores = new Dictionary<string, int> { [PartA] = 0, [PartB] = 0, [PartC] = 0 };
        var jsonPath = Path.Combine(subdir, "content.json");
        var data = File.Exists(jsonPath) ? LoadJson(jsonPath) : null;
        var info = data?.Info;

        if (info?.Video is not null && IsNonEmpty(info.Video)) scores[PartA] += 3;
        if (info?.Question is { Count: > 0 }) scores[PartB] += 3;
        if (info?.Std is { Count: > 0 }) scores[PartC] += 2;
        if (info?.Topic is not null && IsNonEmpty(info.Topic)) scores[PartC] += 2;
        if (!string.IsNullOrEmpty(info?.Analyze)) scores[PartC] += 1;

        var materialDir = Path.Combine(subdir, "material");
        var files = Directory.Exists(materialDir)
            ? Directory.EnumerateFiles(materialDir).Select(Path.GetFileName).ToList()
            : [];
        if (files.Any(n => n is not null && ReVideoTag.IsMatch(n))) scores[PartA] += 2;
        if (files.Any(n => n is not null && RePartBQuesAudio.IsMatch(n))) scores[PartB] += 2;
        if (files.Any(n => n is not null && RePartCQuesAudio.IsMatch(n))) scores[PartC] += 2;

        var best = scores.OrderByDescending(kv => kv.Value).First();
        return best.Value > 0 ? best.Key : null;
    }

    private static void ParseQuestions(List<Question>? questions, List<string> lines)
    {
        if (questions is null || questions.Count == 0)
        {
            lines.Add("未找到问题列表（info.question）");
            return;
        }

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var ask = StripHtmlTags(q.Ask ?? "");
            var answers = (q.Std ?? [])
                .Where(a => a.Value is not null)
                .Take(3)
                .Select(a => StripHtmlTags(a.Value ?? ""))
                .ToList();

            lines.Add($"\n【问题 {i + 1}】 {ask}");
            lines.Add("  候选答案：");
            for (int j = 0; j < answers.Count; j++)
            {
                var parts = answers[j].Split('\n');
                if (parts.Length == 1 && parts[0].Trim().Length == 0)
                {
                    lines.Add($"    {j + 1}. (空)");
                }
                else
                {
                    lines.Add($"    {j + 1}. {parts[0]}");
                    for (int k = 1; k < parts.Length; k++)
                        lines.Add($"       {parts[k]}");
                }
            }
            lines.Add("");
        }
    }

    // ---- 文本处理（与主程序 TextHelper 行为完全一致，保证输出逐字节相同） ----

    /// <summary>先剥离 HTML 标签，再解码 HTML 实体。</summary>
    private static string StripHtmlTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var withoutTags = Regex.Replace(text, @"<[^>]+>", string.Empty);
        try { return System.Net.WebUtility.HtmlDecode(withoutTags); }
        catch { return withoutTags; }
    }

    /// <summary>按 &lt;p&gt; 标签拆段；无闭合对时退回整段（剥标签、解码实体）。</summary>
    private static List<string> SplitHtmlParagraphs(string? htmlText)
    {
        if (string.IsNullOrEmpty(htmlText))
            return [];

        var paragraphs = Regex.Matches(htmlText, @"<p>(.*?)</p>", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => StripHtmlTags(p).Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (paragraphs.Count == 0)
        {
            var whole = StripHtmlTags(htmlText).Trim();
            return whole.Length > 0 ? [whole] : [];
        }
        return paragraphs;
    }

    /// <summary>段落间插入空行。</summary>
    private static string FormatTextWithParagraphs(string? text)
    {
        var paras = SplitHtmlParagraphs(text);
        if (paras.Count == 0)
            return StripHtmlTags(text ?? string.Empty);
        return string.Join("\n\n", paras);
    }

    private static bool IsNonEmpty(object? value)
    {
        if (value is null) return false;
        return value is not string s || !string.IsNullOrWhiteSpace(s);
    }

    private static ContentFragment? LoadJson(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ContentFragment>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    // ---- 自包含的 content.json 模型（插件不依赖主程序内部类型） ----

    private sealed class ContentFragment
    {
        [JsonPropertyName("structure_type")] public string? StructureType { get; set; }
        [JsonPropertyName("info")] public ContentInfo? Info { get; set; }
    }

    private sealed class ContentInfo
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("question")] public List<Question>? Question { get; set; }
        [JsonPropertyName("std")] public List<AnswerValue>? Std { get; set; }
        [JsonPropertyName("video")] public object? Video { get; set; }
        [JsonPropertyName("topic")] public object? Topic { get; set; }
        [JsonPropertyName("analyze")] public string? Analyze { get; set; }
    }

    private sealed class Question
    {
        [JsonPropertyName("ask")] public string? Ask { get; set; }
        [JsonPropertyName("std")] public List<AnswerValue>? Std { get; set; }
    }

    private sealed class AnswerValue
    {
        [JsonPropertyName("value")] public string? Value { get; set; }
    }
}