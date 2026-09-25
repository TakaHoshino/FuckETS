using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FuckETS.Models;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;
using FuckETS.Plugins;
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
    private bool _updatingParserCombo;

    public MainWindow()
    {
        InitializeComponent();
        _pdfOptions = PdfSettingsService.Load();
        PluginHost.EnsureInitialized();
        PluginHost.Current.PluginsChanged += (_, _) => Dispatcher.Invoke(RefreshParserComboBox);
        RefreshParserComboBox();
        Closed += (_, _) => Application.Current.Shutdown();
        Loaded += async (_, _) => await ScanFoldersAsync();
    }

    // ------------------------------------------------------------------
    // 解析插件下拉框
    // ------------------------------------------------------------------

    /// <summary>刷新解析插件下拉框（仅列出启用的解析插件），并恢复/回退当前选择。</summary>
    private void RefreshParserComboBox()
    {
        _updatingParserCombo = true;
        try
        {
            var parsers = PluginHost.Current.GetEnabledParserPlugins();
            ParserComboBox.ItemsSource = parsers;

            var selected = PluginHost.Current.GetSelectedParser(out var fellBack);
            if (fellBack)
            {
                Logger.Warn("原选中的解析插件已失效或被禁用，已回退到默认解析插件。");
                BottomLabel.Text = "提示：原选中的解析插件已失效，已回退到默认解析插件";
            }

            ParserComboBox.SelectedItem = selected is null
                ? null
                : parsers.FirstOrDefault(p => p.Manifest.Id == selected.Manifest.Id);

            if (parsers.Count == 0)
            {
                StatusLabel.Text = "没有可用的解析插件，无法解析";
                Logger.Warn("没有可用的解析插件（内置插件被禁用且无外部解析插件）。");
            }
        }
        finally
        {
            _updatingParserCombo = false;
        }
        UpdateParseButtonState();
    }

    private void ParserComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingParserCombo)
            return;
        if (ParserComboBox.SelectedItem is PluginInfo info)
        {
            PluginHost.Current.SelectParser(info.Manifest.Id);
            Logger.Debug($"解析插件切换为：{info.DisplayName}");
        }
        UpdateParseButtonState();
    }

    /// <summary>解析按钮可用 = 已选中文件夹 且 至少有一个启用的解析插件。</summary>
    private void UpdateParseButtonState()
        => ParseButton.IsEnabled = _selectedFolder is not null && ParserComboBox.SelectedItem is PluginInfo;

    // ------------------------------------------------------------------
    // 插件管理
    // ------------------------------------------------------------------
    private void PluginManagerButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PluginManagerWindow { Owner = this };
        dialog.ShowDialog();
        RefreshParserComboBox();
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

        // 扫描完成钩子：插件可增删/改写扫描到的文件夹信息
        var scanCtx = new ScanCompletedContext();
        foreach (var folder in folders)
            scanCtx.Folders.Add(ToFolderInfo(folder));
        PluginHost.Current.Publish(HookNames.ScanCompleted, scanCtx);
        if (scanCtx.Folders.Count != folders.Count)
        {
            Logger.Warn($"扫描完成钩子修改了文件夹数量（{folders.Count} → {scanCtx.Folders.Count}），仅保留路径有效的条目。");
            var rebuilt = scanCtx.Folders
                .Where(i => !string.IsNullOrEmpty(i.Path) && Directory.Exists(i.Path))
                .Select(i => new FolderItem(i.Name, i.Path, i.CreationTime) { HomeworkTitle = i.HomeworkTitle })
                .ToList();
            folders.Clear();
            folders.AddRange(rebuilt);
        }
        else
        {
            for (int i = 0; i < folders.Count; i++)
                folders[i].HomeworkTitle = scanCtx.Folders[i].HomeworkTitle;
        }

        _folders.Clear();
        _folders.AddRange(folders);
        FolderGrid.ItemsSource = null;
        FolderGrid.ItemsSource = _folders;

        // 文件夹列表加载后钩子（通知型）
        var listCtx = new FolderListLoadedContext();
        foreach (var folder in _folders)
            listCtx.Folders.Add(ToFolderInfo(folder));
        PluginHost.Current.Publish(HookNames.FolderListLoaded, listCtx);

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
        UpdateParseButtonState();
        if (_selectedFolder is null)
            SavePdfButton.IsEnabled = false;
    }

    private static FolderItemInfo ToFolderInfo(FolderItem folder) => new()
    {
        Name = folder.Name,
        Path = folder.Path,
        CreationTime = folder.CreationTime,
        HomeworkTitle = folder.HomeworkTitle,
    };

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

    /// <summary>解析文件夹：调度当前选中的解析插件（含插件识别与钩子），返回保留的答案相关内容。</summary>
    private static List<string> ParseFolder(string folderPath)
    {
        var host = PluginHost.Current;
        var parser = host.GetSelectedParser(out _);
        if (parser is null)
        {
            Logger.Error("解析被阻止：没有可用的解析插件。");
            return ["错误：没有可用的解析插件（请在插件管理中启用至少一个解析插件）"];
        }
        return host.RunParse(parser, folderPath);
    }

    private void DisplayResult(List<string> outputLines)
    {
        // 展示前钩子：插件可改写要展示的行
        var ctx = new ParseResultDisplayingContext();
        ctx.OutputLines.AddRange(outputLines);
        PluginHost.Current.Publish(HookNames.ResultDisplaying, ctx);

        ClearResult();
        foreach (var line in ctx.OutputLines)
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

        // PDF 导出前钩子：插件可改写导出内容、文件名与导出参数
        var pdfCtx = new PdfExportingContext { OutputText = output, FileName = filename };
        AddPdfOption(pdfCtx, "fontSize", options.FontSize);
        AddPdfOption(pdfCtx, "titleFontSize", options.TitleFontSize);
        AddPdfOption(pdfCtx, "partHeadingFontSize", options.PartHeadingFontSize);
        AddPdfOption(pdfCtx, "marginMm", options.MarginMm);
        AddPdfOption(pdfCtx, "lineHeight", options.LineHeight);
        AddPdfOption(pdfCtx, "boldHeadings", options.BoldHeadings);
        PluginHost.Current.Publish(HookNames.PdfExporting, pdfCtx);
        output = pdfCtx.OutputText;
        filename = pdfCtx.FileName;
        ApplyPdfOptions(pdfCtx, options);

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

    /// <summary>追加一个 PDF 导出参数项。</summary>
    private static void AddPdfOption(PdfExportingContext ctx, string key, object value)
        => ctx.Options.Add(new PdfExportOption { Key = key, Value = value });

    /// <summary>将钩子中被插件修改的 PDF 参数写回配置。</summary>
    private static void ApplyPdfOptions(PdfExportingContext ctx, PdfExportOptions options)
    {
        foreach (var opt in ctx.Options)
        {
            try
            {
                switch (opt.Key)
                {
                    case "fontSize": options.FontSize = Convert.ToSingle(opt.Value); break;
                    case "titleFontSize": options.TitleFontSize = Convert.ToSingle(opt.Value); break;
                    case "partHeadingFontSize": options.PartHeadingFontSize = Convert.ToSingle(opt.Value); break;
                    case "marginMm": options.MarginMm = Convert.ToSingle(opt.Value); break;
                    case "lineHeight": options.LineHeight = Convert.ToSingle(opt.Value); break;
                    case "boldHeadings": options.BoldHeadings = Convert.ToBoolean(opt.Value); break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"PDF 导出钩子参数 {opt.Key} 无效，已忽略：{ex.Message}");
            }
        }
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