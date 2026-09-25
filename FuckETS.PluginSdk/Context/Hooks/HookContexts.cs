using FuckETS.PluginSdk.Models;

namespace FuckETS.PluginSdk.Context.Hooks;

/// <summary>文件夹（作业）信息，供钩子读取/修改。</summary>
public sealed class FolderItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string CreationTime { get; set; } = string.Empty;
    public string HomeworkTitle { get; set; } = string.Empty;
}

/// <summary>挂钩点上下文基类。</summary>
public abstract class HookContext
{
    /// <summary>表示某插件是否可以阻止本次后续处理的标志（按需使用）。</summary>
    public bool Cancel { get; set; }
}

/// <summary>扫描完成钩子上下文：承载扫描到的文件夹列表（可修改，如增删/改标题）。</summary>
public sealed class ScanCompletedContext : HookContext
{
    public List<FolderItemInfo> Folders { get; } = new();
}

/// <summary>Part 识别前钩子上下文。</summary>
public sealed class PartDetectBeforeContext : HookContext
{
    public string ContentDir { get; init; } = string.Empty;
    public int ContentId { get; init; }
}

/// <summary>Part 识别后钩子上下文：可修改识别结果（Part、判定依据）。</summary>
public sealed class PartDetectAfterContext : HookContext
{
    public string ContentDir { get; init; } = string.Empty;
    public string? Part { get; set; }
    public List<string> Signals { get; } = new();
}

/// <summary>Part 解析前钩子上下文。</summary>
public sealed class PartParseBeforeContext : HookContext
{
    public string Part { get; init; } = string.Empty;
    public string PartLabel { get; init; } = string.Empty;
    public string JsonPath { get; init; } = string.Empty;
}

/// <summary>Part 解析后钩子上下文：可读取该 Part 解析产出的行。</summary>
public sealed class PartParseAfterContext : HookContext
{
    public string Part { get; init; } = string.Empty;
    public string PartLabel { get; init; } = string.Empty;
    public string JsonPath { get; init; } = string.Empty;
    public List<string> ProducedLines { get; } = new();
}

/// <summary>解析完成钩子上下文：可修改/替换全部输出行。</summary>
public sealed class ParseCompletedContext : HookContext
{
    public List<string> OutputLines { get; } = new();
}

/// <summary>文件夹列表加载后钩子上下文。</summary>
public sealed class FolderListLoadedContext : HookContext
{
    public List<FolderItemInfo> Folders { get; } = new();
}

/// <summary>解析结果展示前钩子上下文：可修改展示的行。</summary>
public sealed class ParseResultDisplayingContext : HookContext
{
    public List<string> OutputLines { get; } = new();
}

/// <summary>PDF 导出前钩子上下文：可修改导出内容或选项。</summary>
public sealed class PdfExportingContext : HookContext
{
    public string OutputText { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public IList<PdfExportOption> Options { get; } = new List<PdfExportOption>();
}

/// <summary>PDF 导出可调参数项（供插件读取/覆盖）。</summary>
public sealed class PdfExportOption
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
}