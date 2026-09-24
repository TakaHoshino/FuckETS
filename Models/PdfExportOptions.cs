namespace FuckETS.Models;

/// <summary>PDF 导出参数配置。</summary>
public sealed class PdfExportOptions
{
    /// <summary>正文字号（pt）。默认 20。</summary>
    public float FontSize { get; set; } = 20f;

    /// <summary>作业标题字号（pt）。默认 24。</summary>
    public float TitleFontSize { get; set; } = 24f;

    /// <summary>Part 标题字号（pt）。默认 22。</summary>
    public float PartHeadingFontSize { get; set; } = 22f;

    /// <summary>页边距（mm）。默认 20。</summary>
    public float MarginMm { get; set; } = 20f;

    /// <summary>行间距倍率。默认 1.2。</summary>
    public float LineHeight { get; set; } = 1.2f;

    /// <summary>是否使用粗体标题（可选扩展）。默认 false 保留既有样式。</summary>
    public bool BoldHeadings { get; set; }

    /// <summary>创建一个与既有样式接近的默认配置。</summary>
    public static PdfExportOptions Default() => new();
}