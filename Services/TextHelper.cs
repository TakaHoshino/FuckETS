using System.Net;
using System.Text.RegularExpressions;

namespace FuckETS.Services;

/// <summary>文本处理工具：HTML 标签剥离、实体解码与段落格式化。</summary>
public static class TextHelper
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>剥离所有 HTML 标签，并将常见的 HTML 实体（如 &amp;、&lt;、&nbsp; 等）解码为对应字符。</summary>
    public static string StripHtmlTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var withoutTags = HtmlTagRegex.Replace(text, string.Empty);
        return DecodeHtmlEntities(withoutTags);
    }

    /// <summary>解码 HTML 实体（&amp; &lt; &gt; &quot; &#39; &nbsp; 及数字/十六进制实体）。</summary>
    public static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        try
        {
            return WebUtility.HtmlDecode(text);
        }
        catch
        {
            return text;
        }
    }

    /// <summary>按 &lt;p&gt; 标签拆分为段落列表；无 &lt;p&gt; 闭合对时退回整段（已剥标签、解码实体）。</summary>
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
        {
            // 找不到闭合的 <p></p>（例如标签残缺、无 <p> 包裹），整段剥离 HTML 标签后作为单一段落返回。
            var whole = StripHtmlTags(htmlText).Trim();
            return whole.Length > 0 ? [whole] : [];
        }

        return paragraphs;
    }

    /// <summary>段落间插入空行，提升可读性（与原 format_text_with_paragraphs 等价）。</summary>
    public static string FormatTextWithParagraphs(string? text)
    {
        var paras = SplitHtmlParagraphs(text);
        if (paras.Count == 0)
            return StripHtmlTags(text ?? string.Empty);
        return string.Join("\n\n", paras);
    }
}