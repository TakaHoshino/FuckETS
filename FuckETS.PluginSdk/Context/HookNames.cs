namespace FuckETS.PluginSdk.Context;

/// <summary>
/// 主程序预留的钩子点（Hook）名称目录。插件通过 <c>PluginManager.Subscribe</c> 订阅。
/// 各钩子的签名见 <see cref="HookNames"/> 或 SDK 文档。
/// </summary>
public static class HookNames
{
    // ---- 解析流程 ----
    /// <summary>扫描完成后触发。可修改扫描到的文件夹列表（FolderListContext）。</summary>
    public const string ScanCompleted = "scan.completed";

    /// <summary>对某个 content_* 子目录进行 Part 识别前触发（PartDetectBeforeContext）。</summary>
    public const string PartDetectBefore = "part.detect.before";

    /// <summary>Part 识别后触发（PartDetectAfterContext）。</summary>
    public const string PartDetectAfter = "part.detect.after";

    /// <summary>某个 Part 解析前触发（PartParseBeforeContext）。</summary>
    public const string PartParseBefore = "part.parse.before";

    /// <summary>某个 Part 解析后触发（PartParseAfterContext）。</summary>
    public const string PartParseAfter = "part.parse.after";

    /// <summary>整个解析完成后触发（ParseCompletedContext，可修改/替换输出行）。</summary>
    public const string ParseCompleted = "parse.completed";

    // ---- 界面流程 ----
    /// <summary>文件夹列表加载后触发（FolderListContext）。</summary>
    public const string FolderListLoaded = "ui.folderList.loaded";

    /// <summary>解析结果展示前触发（ParseResultDisplayingContext，可修改展示行）。</summary>
    public const string ResultDisplaying = "ui.result.displaying";

    /// <summary>PDF 导出前触发（PdfExportingContext，可修改导出内容或选项）。</summary>
    public const string PdfExporting = "ui.pdf.exporting";
}