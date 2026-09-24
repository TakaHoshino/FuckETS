using System.Windows;
using FuckETS.Models;
using FuckETS.Services;

namespace FuckETS;

/// <summary>PDF 导出设置弹窗。输入非法时给出提示，不影响主界面。</summary>
public partial class PdfSettingsWindow : Window
{
    private PdfExportOptions _options = PdfExportOptions.Default();

    public PdfSettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>外部传入并在窗口打开前显示的 PDF 配置。</summary>
    public PdfExportOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            PopulateFields();
        }
    }

    private void PopulateFields()
    {
        FontSizeBox.Text = _options.FontSize.ToString("0.##");
        TitleFontSizeBox.Text = _options.TitleFontSize.ToString("0.##");
        PartHeadingFontSizeBox.Text = _options.PartHeadingFontSize.ToString("0.##");
        MarginBox.Text = _options.MarginMm.ToString("0.##");
        LineHeightBox.Text = _options.LineHeight.ToString("0.##");
        BoldHeadingsCheck.IsChecked = _options.BoldHeadings;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadFloat(FontSizeBox, out var fontSize, "正文字号") ||
            !TryReadFloat(TitleFontSizeBox, out var titleFontSize, "作业标题字号") ||
            !TryReadFloat(PartHeadingFontSizeBox, out var partHeadingFontSize, "Part 标题字号") ||
            !TryReadFloat(MarginBox, out var marginMm, "页边距") ||
            !TryReadFloat(LineHeightBox, out var lineHeight, "行间距"))
        {
            return;
        }

        // 简单范围校验，避免非法值导致 PDF 生成异常
        _options.FontSize = Math.Clamp(fontSize, 8f, 72f);
        _options.TitleFontSize = Math.Clamp(titleFontSize, 10f, 96f);
        _options.PartHeadingFontSize = Math.Clamp(partHeadingFontSize, 10f, 72f);
        _options.MarginMm = Math.Clamp(marginMm, 5f, 50f);
        _options.LineHeight = Math.Clamp(lineHeight, 1f, 3f);
        _options.BoldHeadings = BoldHeadingsCheck.IsChecked == true;

        PdfSettingsService.Save(_options);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private bool TryReadFloat(System.Windows.Controls.TextBox box, out float value, string label)
    {
        if (float.TryParse(box.Text.Trim(), out var v) && float.IsFinite(v))
        {
            value = v;
            return true;
        }

        MessageBox.Show($"“{label}”不是有效的数值，请输入数字。", "提示",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        box.Focus();
        value = 0;
        return false;
    }
}