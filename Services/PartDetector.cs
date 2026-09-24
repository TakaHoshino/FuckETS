using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using FuckETS.Models;

namespace FuckETS.Services;

/// <summary>Part 常量与类型识别。</summary>
public static class PartDetector
{
    public const string PartA = "PartA";
    public const string PartB = "PartB";
    public const string PartC = "PartC";

    /// <summary>structure_type 是最可靠的特征。</summary>
    private static readonly Dictionary<string, string> StructureTypeMap = new()
    {
        ["collector.read"] = PartA,
        ["collector.3q5a"] = PartB,
        ["collector.picture"] = PartC,
    };

    // 文件结构特征
    private static readonly Regex ReVideo = new(@"\.mp4$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RePartBQuesAudio = new(@"^ques\d+.*\.mp3$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RePartCQuesAudio = new(@"^quesStd\d+\.mp3$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>识别 content_* 子文件夹属于哪个 Part。
    /// 返回 (part, signals)；无法识别时 part 为 null。</summary>
    public static (string? Part, List<string> Signals) Detect(string subdir)
    {
        (string? part, string? stEvidence) = DetectByStructureType(subdir);
        if (part is not null)
        {
            Logger.Debug($"PartDetector：{Path.GetFileName(subdir)} → {part}（结构类型）。");
            return (part, [$"结构类型：{stEvidence}"]);
        }

        (part, var signals) = DetectByFeatures(subdir);
        if (part is not null)
        {
            Logger.Debug($"PartDetector：{Path.GetFileName(subdir)} → {part}（特征判定）。");
            return (part, signals);
        }

        Logger.Debug($"PartDetector：{Path.GetFileName(subdir)} → 未识别。");
        return (null, ["未识别出所属部分（缺少 content.json 或特征不足）"]);
    }

    /// <summary>依据 content.json 的 structure_type 判定。返回 (part, 依据)。</summary>
    private static (string? Part, string? Evidence) DetectByStructureType(string subdir)
    {
        var jsonPath = Path.Combine(subdir, "content.json");
        if (!File.Exists(jsonPath))
            return (null, null);

        var data = LoadJson(jsonPath);
        if (data is null || data.StructureType is null)
            return (null, null);

        var st = data.StructureType;
        if (StructureTypeMap.TryGetValue(st, out var part))
            return (part, $"structure_type = {st}");
        return (null, null);
    }

    /// <summary>收集内容字段与文件结构特征，用于评分。返回 (scores, signals)。</summary>
    private static (Dictionary<string, int> Scores, List<string> Signals) CollectFeatureSignals(string subdir)
    {
        var scores = new Dictionary<string, int> { [PartA] = 0, [PartB] = 0, [PartC] = 0 };
        var signals = new List<string>();

        var jsonPath = Path.Combine(subdir, "content.json");
        var data = File.Exists(jsonPath) ? LoadJson(jsonPath) : null;
        var info = data?.Info;

        if (info?.Video is not null)
        {
            if (IsNonEmpty(info.Video))
            {
                scores[PartA] += 3;
                signals.Add("存在 info.video 字段（含视频）");
            }
        }

        if (info?.Question is { Count: > 0 })
        {
            scores[PartB] += 3;
            signals.Add($"存在 info.question 问题列表（{info.Question.Count} 题）");
        }

        if (info?.Std is { Count: > 0 })
        {
            scores[PartC] += 2;
            signals.Add($"存在 info.std 参考答案列表（{info.Std.Count} 条）");
        }

        if (info?.Topic is not null && IsNonEmpty(info.Topic))
        {
            scores[PartC] += 2;
            signals.Add("存在 info.topic 主题字段");
        }

        if (!string.IsNullOrEmpty(info?.Analyze))
        {
            scores[PartC] += 1;
            signals.Add("存在 info.analyze 要点解析");
        }

        var materialDir = Path.Combine(subdir, "material");
        var materialFiles = Directory.Exists(materialDir)
            ? Directory.EnumerateFiles(materialDir).Select(Path.GetFileName).ToList()
            : [];

        if (materialFiles.Any(name => name is not null && ReVideo.IsMatch(name)))
        {
            scores[PartA] += 2;
            signals.Add("material 中存在 .mp4 视频文件");
        }
        if (materialFiles.Any(name => name is not null && RePartBQuesAudio.IsMatch(name)))
        {
            scores[PartB] += 2;
            signals.Add("material 中存在逐题问答音频（quesNaskaudio / quesNStd1 等）");
        }
        if (materialFiles.Any(name => name is not null && RePartCQuesAudio.IsMatch(name)))
        {
            scores[PartC] += 2;
            signals.Add("material 中存在复述参考音频（quesStdN.mp3）");
        }

        return (scores, signals);
    }

    /// <summary>依据内容字段与文件结构特征评分判定。返回 (part, signals)。</summary>
    private static (string? Part, List<string> Signals) DetectByFeatures(string subdir)
    {
        var (scores, signals) = CollectFeatureSignals(subdir);

        string? best = null;
        int bestScore = int.MinValue;
        foreach (var (key, value) in scores)
        {
            if (value > bestScore)
            {
                bestScore = value;
                best = key;
            }
        }

        if (best is not null && scores[best] > 0)
        {
            signals.Insert(0, "未识别到标准 structure_type，依据文件内容及结构特征判定");
            return (best, signals);
        }

        return (null, signals);
    }

    private static ContentData? LoadJson(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<ContentData>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>判断对象是否非空（字符串非空白、或其他非 null 值）。</summary>
    private static bool IsNonEmpty(object? value)
    {
        if (value is null)
            return false;
        if (value is string s)
            return !string.IsNullOrWhiteSpace(s);
        return true;
    }
}