using System.Text.RegularExpressions;

namespace FuckETS.Services;

/// <summary>文本处理工具：HTML 标签剥离与段落格式化。</summary>
public static class TextHelper
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>剥离所有 HTML 标签。</summary>
    public static string StripHtmlTags(string? text) => string.IsNullOrEmpty(text)
        ? string.Empty
        : HtmlTagRegex.Replace(text, string.Empty);

    /// <summary>按 &lt;p&gt; 标签拆分为段落列表；无 &lt;p&gt; 时返回整段。</summary>
    public static List<string> SplitHtmlParagraphs(string? htmlText)
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
            return [htmlText.Trim()];

        return paragraphs;
    }

    /// <summary>段落间插入空行，提升可读性（与原 format_text_with_paragraphs 等价）。</summary>
    public static string FormatTextWithParagraphs(string? text)
    {
        var paras = SplitHtmlParagraphs(text);
        if (paras.Count == 0)
            return text ?? string.Empty;
        return string.Join("\n\n", paras);
    }
}