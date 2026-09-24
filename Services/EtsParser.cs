using System.IO;
using System.Text.Json;
using FuckETS.Models;

namespace FuckETS.Services;

/// <summary>解析 content.json，生成格式化文本行。</summary>
public static class EtsParser
{
    /// <summary>解析单个 content.json 文件，将结果追加至 outputLines。
    /// JSON 缺失或解析异常时输出错误信息，不会抛出中断。</summary>
    public static void Parse(string jsonPath, List<string> outputLines, string partLabel)
    {
        var partNameMap = new Dictionary<string, string>
        {
            ["PartA"] = "模仿朗读",
            ["PartB"] = "角色扮演",
            ["PartC"] = "故事复述",
        };

        Logger.Debug($"开始解析 {partLabel}：{jsonPath}");
        outputLines.Add($"\n【{partLabel}】 {partNameMap.GetValueOrDefault(partLabel, partLabel)}");

        if (!File.Exists(jsonPath))
        {
            outputLines.Add($"错误：未找到 {jsonPath}");
            Logger.Warn($"解析 {partLabel}：文件不存在 {jsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<ContentData>(json);
            var info = data?.Info;

            switch (partLabel)
            {
                case "PartA":
                case "PartC":
                    ParseContentValue(info?.Value, outputLines, partLabel);
                    break;
                case "PartB":
                    ParsePartB(info?.Question, outputLines);
                    break;
            }
        }
        catch (Exception ex)
        {
            outputLines.Add($"[{partLabel} 解析错误] {ex.Message}");
            Logger.Error($"解析 {partLabel} 整体出错：{ex.Message}", ex);
        }
    }

    /// <summary>PartA / PartC：取 info.value 字段并按段落格式化。</summary>
    private static void ParseContentValue(string? value, List<string> outputLines, string partLabel)
    {
        try
        {
            var cleanText = TextHelper.FormatTextWithParagraphs(value);
            outputLines.Add(cleanText);
        }
        catch (Exception ex)
        {
            outputLines.Add($"[{partLabel} 解析错误] {ex.Message}");
        }
    }

    /// <summary>PartB：逐问题输出 ask 及 std[] 前 3 个标准答案。</summary>
    private static void ParsePartB(List<Question>? questions, List<string> outputLines)
    {
        try
        {
            if (questions is null || questions.Count == 0)
            {
                outputLines.Add("未找到问题列表（info.question）");
                Logger.Warn("PartB 未找到问题列表（info.question）。");
                return;
            }

            for (int idx = 0; idx < questions.Count; idx++)
            {
                var qa = questions[idx];
                var ask = TextHelper.StripHtmlTags(qa.Ask);
                var answers = (qa.Std ?? [])
                    .Where(a => a.Value is not null)
                    .Take(3)
                    .Select(a => TextHelper.StripHtmlTags(a.Value))
                    .ToList();
                Logger.Debug($"PartB 问题 {idx + 1}：共 {answers.Count} 个候选答案。");

                outputLines.Add($"\n【问题 {idx + 1}】 {ask}");
                outputLines.Add("  候选答案：");

                for (int i = 0; i < answers.Count; i++)
                {
                    var lines = answers[i].Split('\n');
                    if (lines is { Length: 0 } || (lines.Length == 1 && lines[0].Trim().Length == 0))
                    {
                        outputLines.Add($"    {i + 1}. (空)");
                    }
                    else
                    {
                        outputLines.Add($"    {i + 1}. {lines[0]}");
                        for (int j = 1; j < lines.Length; j++)
                        {
                            outputLines.Add($"       {lines[j]}");
                        }
                    }
                }

                outputLines.Add("");
            }
        }
        catch (Exception ex)
        {
            outputLines.Add($"[PartB 解析错误] {ex.Message}");
            Logger.Error($"PartB 解析出错：{ex.Message}", ex);
        }
    }
}