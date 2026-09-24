using System.Text.RegularExpressions;

namespace FuckETS.Services;

/// <summary>行分类：决定文本区的着色样式标签。</summary>
public static class LineClassifier
{
    /// <summary>样式标签枚举。</summary>
    public enum Category
    {
        Title,
        PartHeading,
        Question,
        AnswerCandidate,
        Info,
        Normal,
    }

    /// <summary>将一行文本归类为样式标签。</summary>
    public static Category ClassifyLine(string line)
    {
        var stripped = line.Trim();
        if (stripped.Length == 0)
            return Category.Normal;

        if (line.StartsWith("作业文件夹：") || line.StartsWith("作业标题："))
            return Category.Title;

        if (PartHeadingRegex.IsMatch(stripped))
            return Category.PartHeading;

        if (QuestionRegex.IsMatch(stripped))
            return Category.Question;

        if (stripped.StartsWith("候选答案：") || AnswerStartRegex.IsMatch(stripped))
            return Category.AnswerCandidate;

        if (stripped.StartsWith("[完成]") || stripped.StartsWith("警告：") || stripped.StartsWith("错误："))
            return Category.Info;

        return Category.Normal;
    }

    // ^【PartA】 或 [PartA] 或 PartA：
    private static readonly Regex PartHeadingRegex = new(
        @"^[\【\[]\s*Part[A-C]\s*[\】\]]|^Part[A-C]\s*[：:]",
        RegexOptions.Compiled);

    // ^【问题 1】 或 [问题 1] 或 问题 1：
    private static readonly Regex QuestionRegex = new(
        @"^[\【\[]\s*问题\s*\d+\s*[\】\]]|^问题\s*\d+\s*[：:]",
        RegexOptions.Compiled);

    // ^1. 或 ^  2.
    private static readonly Regex AnswerStartRegex = new(
        @"^\s*\d+\.",
        RegexOptions.Compiled);
}