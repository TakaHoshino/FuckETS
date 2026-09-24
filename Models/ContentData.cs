using System.Text.Json.Serialization;

namespace FuckETS.Models;

/// <summary>单个 content.json 的根部数据结构。</summary>
public sealed class ContentData
{
    /// <summary>服务器端写入的结构类型（用于 Part 识别）。</summary>
    [JsonPropertyName("structure_type")]
    public string? StructureType { get; set; }

    [JsonPropertyName("info")]
    public Info? Info { get; set; }
}

public sealed class Info
{
    /// <summary>PartA / PartC 的正文内容（HTML）。</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>PartB 的问题列表。</summary>
    [JsonPropertyName("question")]
    public List<Question>? Question { get; set; }

    /// <summary>PartC 顶层的参考答案列表。</summary>
    [JsonPropertyName("std")]
    public List<StdAnswer>? Std { get; set; }

    /// <summary>视频字段（PartA 特征，可为 URL/对象/数组等）。</summary>
    [JsonPropertyName("video")]
    public object? Video { get; set; }

    /// <summary>主题字段（PartC 特征）。</summary>
    [JsonPropertyName("topic")]
    public object? Topic { get; set; }

    /// <summary>要点解析（PartC 特征）。</summary>
    [JsonPropertyName("analyze")]
    public string? Analyze { get; set; }
}

public sealed class Question
{
    /// <summary>问题文本（HTML）。</summary>
    [JsonPropertyName("ask")]
    public string? Ask { get; set; }

    /// <summary>标准答案列表。</summary>
    [JsonPropertyName("std")]
    public List<StdAnswer>? Std { get; set; }
}

public sealed class StdAnswer
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}