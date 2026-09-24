using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FuckETS.Models;
using FuckETS.Services;

namespace FuckETS;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<FolderItem> _folders = new();
    private FolderItem? _selectedFolder;
    private string _currentOutput = string.Empty;
    private PdfExportOptions _pdfOptions = PdfExportOptions.Default();

    public MainWindow()
    {
        InitializeComponent();
        _pdfOptions = PdfSettingsService.Load();
        Closed += (_, _) => Application.Current.Shutdown();
        Loaded += async (_, _) => await ScanFoldersAsync();
    }

    // ------------------------------------------------------------------
    // 扫描文件夹
    // ------------------------------------------------------------------
    private async void ScanButton_Click(object sender, RoutedEventArgs e) => await ScanFoldersAsync();

    private async Task ScanFoldersAsync()
    {
        ScanButton.IsEnabled = false;
        ParseButton.IsEnabled = false;
        SavePdfButton.IsEnabled = false;
        _selectedFolder = null;
        ClearResult();
        StatusLabel.Text = "正在扫描...";
        BottomLabel.Text = "";
        Logger.Info("开始扫描作业文件夹。");

        var baseDir = EtsScanner.GetBaseDirectory(out var error);
        if (baseDir is null)
        {
            Logger.Error($"扫描失败：{error}");
            MessageBox.Show($"错误：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusLabel.Text = $"错误：{error}";
            ScanButton.IsEnabled = true;
            return;
        }

        var folders = await Task.Run(() => EtsScanner.Scan(baseDir));

        if (folders.Count == 0)
        {
            Logger.Warn("扫描完成，但未找到任何作业文件夹。");
            StatusLabel.Text = "未找到作业文件夹";
            MessageBox.Show("未找到作业名称的文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ScanButton.IsEnabled = true;
            return;
        }

        // 从 ETS 日志获取作业标题
        var logsDir = await Task.Run(() => HomeworkTitleService.FindEtsLogsDir());
        if (logsDir is not null)
        {
            var neededIds = folders.Select(f => f.Name).ToHashSet();
            var titles = await Task.Run(() => HomeworkTitleService.LoadHomeworkTitles(logsDir, neededIds));
            foreach (var folder in folders)
            {
                folder.HomeworkTitle = titles.GetValueOrDefault(folder.Name, string.Empty);
            }
        }
        else
        {
            Logger.Warn("未找到 ETS 日志目录，无法加载作业标题。");
            BottomLabel.Text = "提示：未找到 ETS 日志目录，无法显示作业标题";
        }

        _folders.Clear();
        _folders.AddRange(folders);
        FolderGrid.ItemsSource = null;
        FolderGrid.ItemsSource = _folders;
        StatusLabel.Text = $"扫描完成，找到 {_folders.Count} 个文件夹";
        Logger.Info($"扫描完成，找到 {_folders.Count} 个作业文件夹。");
        ScanButton.IsEnabled = true;
    }

    // ------------------------------------------------------------------
    // 选择文件夹
    // ------------------------------------------------------------------
    private void FolderGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedFolder = FolderGrid.SelectedItem as FolderItem;
        ParseButton.IsEnabled = _selectedFolder is not null;
        if (_selectedFolder is null)
            SavePdfButton.IsEnabled = false;
    }

    // ------------------------------------------------------------------
    // 解析
    // ------------------------------------------------------------------
    private async void ParseButton_Click(object sender, RoutedEventArgs e) => await ParseSelectedFolderAsync();

    private async Task ParseSelectedFolderAsync()
    {
        if (_selectedFolder is null)
            return;

        ParseButton.IsEnabled = false;
        SavePdfButton.IsEnabled = false;
        ClearResult();
        StatusLabel.Text = "解析中...";
        BottomLabel.Text = "";
        Logger.Info($"开始解析所选文件夹：{_selectedFolder.Name}");

        var folderPath = _selectedFolder.Path;
        var outputLines = await Task.Run(() => ParseFolder(folderPath));

        _currentOutput = string.Join("\n", outputLines);
        DisplayResult(outputLines);

        ParseButton.IsEnabled = true;
        SavePdfButton.IsEnabled = true;
        StatusLabel.Text = "解析完成";
        Logger.Info($"解析完成，共 {outputLines.Count} 行输出。");
    }

    /// <summary>解析文件夹：对每个 content_* 子目录动态识别 Part 并输出答案；仅返回保留的答案相关内容。</summary>
    private static List<string> ParseFolder(string folderPath)
    {
        var outputLines = new List<string>();
        var contentPattern = new Regex(@"^content_(\d+)$");
        var matches = new List<(int Id, string DirPath)>();

        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(folderPath))
            {
                var m = contentPattern.Match(Path.GetFileName(dirPath));
                if (m.Success)
                    matches.Add((int.Parse(m.Groups[1].Value), dirPath));
            }
        }
        catch (Exception ex)
        {
            outputLines.Add($"读取子文件夹出错：{ex.Message}");
            return outputLines;
        }

        var candidates = new Dictionary<string, List<(int Id, string DirPath)>>();
        foreach (var (id, dirPath) in matches)
        {
            var (part, _) = PartDetector.Detect(dirPath);
            if (part is null)
                continue;
            if (!candidates.TryGetValue(part, out var list))
            {
                list = new List<(int, string)>();
                candidates[part] = list;
            }
            list.Add((id, dirPath));
        }
        Logger.Debug($"解析：找到 {matches.Count} 个 content_* 子目录；识别结果 PartA={(candidates.TryGetValue(PartDetector.PartA, out var a) ? a.Count : 0)}，" +
                     $"PartB={(candidates.TryGetValue(PartDetector.PartB, out var b) ? b.Count : 0)}，" +
                     $"PartC={(candidates.TryGetValue(PartDetector.PartC, out var c) ? c.Count : 0)}。");

        var chosen = new Dictionary<string, string>();
        foreach (var part in new[] { PartDetector.PartA, PartDetector.PartB, PartDetector.PartC })
        {
            if (candidates.TryGetValue(part, out var list) && list.Count > 0)
            {
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                chosen[part] = list[^1].DirPath;
            }
        }

        foreach (var part in new[] { PartDetector.PartA, PartDetector.PartB, PartDetector.PartC })
        {
            if (chosen.TryGetValue(part, out var subfolder))
            {
                var jsonFile = Path.Combine(subfolder, "content.json");
                EtsParser.Parse(jsonFile, outputLines, part);
            }
            else
            {
                outputLines.Add($"\n【{part}】 未找到对应的子文件夹");
                Logger.Warn($"解析：未识别到 {part} 对应的子文件夹。");
            }
        }

        return outputLines;
    }

    private void DisplayResult(List<string> outputLines)
    {
        ClearResult();
        foreach (var line in outputLines)
        {
            AppOutputLine(line, LineClassifier.ClassifyLine(line));
        }
        ResultTextBox.ScrollToEnd();
    }

    // ------------------------------------------------------------------
    // 结果展示（RichTextBox 着色）
    // ------------------------------------------------------------------
    private void ClearResult()
    {
        ResultTextBox.Document.Blocks.Clear();
    }

    private void AppOutputLine(string line, LineClassifier.Category category)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            ResultTextBox.Document.Blocks.Add(new Paragraph());
            return;
        }

        var (fontSize, bold, foreground, margin) = CategoryStyle(category);

        var run = new Run(line)
        {
            FontSize = fontSize,
            Foreground = new SolidColorBrush(foreground),
        };
        if (bold)
            run.FontWeight = FontWeights.Bold;

        var para = new Paragraph(run)
        {
            Margin = new Thickness(margin, 0, 0, 2),
        };
        ResultTextBox.Document.Blocks.Add(para);
    }

    private static (double FontSize, bool Bold, Color Foreground, double Margin) CategoryStyle(LineClassifier.Category category)
    {
        return category switch
        {
            LineClassifier.Category.Title => (16, true, Color.FromRgb(0x2c, 0x3e, 0x50), 0),
            LineClassifier.Category.PartHeading => (15, true, Color.FromRgb(0x29, 0x80, 0xb9), 0),
            LineClassifier.Category.Question => (14, true, Color.FromRgb(0xc0, 0x39, 0x2b), 12),
            LineClassifier.Category.AnswerCandidate => (13, false, Color.FromRgb(0x27, 0xae, 0x60), 24),
            LineClassifier.Category.Info => (12, false, Color.FromRgb(0x7f, 0x8c, 0x8d), 0),
            _ => (13, false, Colors.Black, 12),
        };
    }

    // ------------------------------------------------------------------
    // PDF 导出设置
    // ------------------------------------------------------------------
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettingsDialog();

    private void ShowSettingsDialog()
    {
        var dialog = new PdfSettingsWindow
        {
            Owner = this,
            Options = _pdfOptions,
        };
        if (dialog.ShowDialog() == true)
        {
            _pdfOptions = dialog.Options;
        }
    }

    // ------------------------------------------------------------------
    // 保存 PDF
    // ------------------------------------------------------------------
    private async void SavePdfButton_Click(object sender, RoutedEventArgs e) => await ExportPdfAsync();

    private async Task ExportPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentOutput))
        {
            Logger.Warn("尝试导出 PDF，但当前没有可保存的解析内容。");
            MessageBox.Show("没有可保存的内容，请先解析一个文件夹。", "警告",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var suggestedName = GetDefaultFileName();

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存 PDF 文件",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = suggestedName + ".pdf",
        };

        if (dialog.ShowDialog(this) != true)
        {
            Logger.Debug("用户取消了 PDF 保存对话框。");
            return;
        }

        var filename = dialog.FileName;
        var output = _currentOutput;
        var options = _pdfOptions;
        SavePdfButton.IsEnabled = false;
        StatusLabel.Text = "正在生成 PDF...";
        Logger.Info($"开始生成 PDF：{filename}（正文字号={options.FontSize}，边距={options.MarginMm}mm）");

        var (success, error) = await Task.Run(() => PdfService.Generate(output, filename, options));

        if (success)
        {
            StatusLabel.Text = $"PDF 已保存: {Path.GetFileName(filename)}";
            Logger.Info($"PDF 生成成功：{filename}");
            MessageBox.Show($"PDF 已保存至:{Environment.NewLine}{filename}", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusLabel.Text = "PDF 生成失败";
            Logger.Error($"PDF 生成失败：{error}");
            MessageBox.Show($"PDF 生成失败：{Environment.NewLine}{error}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        SavePdfButton.IsEnabled = true;
    }

    /// <summary>净化字符串为合法文件名（替换非法字符为空）。</summary>
    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        var bad = new Regex(@"[\\/:*?\""<>|]", RegexOptions.Compiled);
        return bad.Replace(name, string.Empty).Trim();
    }

    /// <summary>生成默认导出文件名：作业标题（净化）或文件夹名称。</summary>
    private string GetDefaultFileName()
    {
        if (_selectedFolder is not null)
        {
            var title = SanitizeFileName(_selectedFolder.HomeworkTitle);
            if (!string.IsNullOrEmpty(title))
                return title;
            var folderName = SanitizeFileName(_selectedFolder.Name);
            if (!string.IsNullOrEmpty(folderName))
                return folderName;
        }
        return "FuckETS";
    }
}