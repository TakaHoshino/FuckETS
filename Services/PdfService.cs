using System.IO;
using FuckETS.Models;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FuckETS.Services;

/// <summary>使用 QuestPDF 将解析结果渲染为 A4 PDF。</summary>
public static class PdfService
{
    private const string ChineseFontName = "ChineseFont";

    /// <summary>生成 PDF，返回 (是否成功, 错误信息)。使用默认导出配置。</summary>
    public static (bool Success, string ErrorMessage) Generate(string outputText, string filename)
        => Generate(outputText, filename, PdfExportOptions.Default());

    /// <summary>生成 PDF，返回 (是否成功, 错误信息)。</summary>
    public static (bool Success, string ErrorMessage) Generate(string outputText, string filename, PdfExportOptions options)
    {
        Logger.Debug($"开始生成 PDF：{filename}");
        var fontPath = SystemHelper.FindChineseFont();
        if (fontPath is null)
        {
            Logger.Error("未找到中文字体，无法生成 PDF。");
            return (false, "未找到中文字体文件，无法生成 PDF");
        }

        QuestPDF.Settings.License = LicenseType.Community;

        try
        {
            using var fontStream = File.OpenRead(fontPath);
            FontManager.RegisterFontWithCustomName(ChineseFontName, fontStream);
        }
        catch (Exception ex)
        {
            Logger.Error($"加载中文字体失败：{fontPath}", ex);
            return (false, $"加载中文字体失败: {fontPath}\n{ex.Message}");
        }

        // 将输出打包为 (文本, 分类) 行列表，空行也保留以维持间距。
        var entries = outputText
            .Split('\n')
            .Select(text => (Text: text, Category: LineClassifier.ClassifyLine(text)))
            .ToList();

        // 从配置读取字号与行距
        var bodyFontSize = options.FontSize;
        var titleFontSize = options.TitleFontSize;
        var partHeadingFontSize = options.PartHeadingFontSize;
        var infoFontSize = Math.Max(8f, bodyFontSize - 1f);
        var marginMm = options.MarginMm;
        var lineHeight = options.LineHeight;
        var boldHeadings = options.BoldHeadings;

        try
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(marginMm, Unit.Millimetre);
                    page.DefaultTextStyle(t => t
                        .FontFamily(ChineseFontName)
                        .FontSize(bodyFontSize)
                        .LineHeight(lineHeight));

                    page.Content().Column(col =>
                    {
                        foreach (var entry in entries)
                        {
                            if (entry.Text.Trim().Length == 0)
                            {
                                col.Item().Height(4);
                                continue;
                            }

                            var item = col.Item();
                            switch (entry.Category)
                            {
                                case LineClassifier.Category.Title:
                                    var titleText = item.Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(titleFontSize)
                                        .FontColor(Colors.BlueGrey.Darken3);
                                    if (boldHeadings)
                                        titleText.Bold();
                                    break;
                                case LineClassifier.Category.PartHeading:
                                    var partText = item.Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(partHeadingFontSize)
                                        .FontColor(Colors.Blue.Medium);
                                    if (boldHeadings)
                                        partText.Bold();
                                    break;
                                case LineClassifier.Category.Question:
                                    item.PaddingLeft(20).Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(bodyFontSize).Bold()
                                        .FontColor(Colors.Red.Medium);
                                    break;
                                case LineClassifier.Category.AnswerCandidate:
                                    item.PaddingLeft(40).Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(bodyFontSize)
                                        .FontColor(Colors.Green.Medium);
                                    break;
                                case LineClassifier.Category.Info:
                                    item.Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(infoFontSize)
                                        .FontColor(Colors.Grey.Medium);
                                    break;
                                default:
                                    item.PaddingLeft(20).Text(entry.Text)
                                        .FontFamily(ChineseFontName).FontSize(bodyFontSize);
                                    break;
                            }
                        }
                    });
                });
            });

            doc.GeneratePdf(filename);
            Logger.Debug($"PDF 生成完成：{filename}");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Logger.Error($"PDF 生成错误：{ex.Message}", ex);
            return (false, $"PDF 生成错误：{ex.Message}");
        }
    }
}