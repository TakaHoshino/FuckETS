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

/// <summary>
/// 主界面工具栏扩展钩子上下文：插件通过 <see cref="AddButton"/> 追加自定义按钮，
/// 主程序读取 <see cref="Buttons"/> 实例化为工具栏按钮（插件因此无需依赖 WPF）。
/// </summary>
public sealed class MainWindowToolbarContext : HookContext
{
    private readonly List<ToolbarButtonInfo> _buttons = new();

    /// <summary>插件追加的按钮列表（主程序读取并实例化为工具栏按钮）。</summary>
    public IReadOnlyList<ToolbarButtonInfo> Buttons => _buttons;

    /// <summary>由主程序在投递钩子前注入：在 UI 线程弹出信息框（参数：标题、内容）。插件在按钮回调中调用。</summary>
    public Action<string, string>? ShowMessage { get; set; }

    /// <summary>追加一个工具栏按钮；onClick 由主程序保证在 UI 线程执行。</summary>
    public void AddButton(string text, Action onClick, string? toolTip = null)
        => _buttons.Add(new ToolbarButtonInfo { Text = text, Clicked = onClick, ToolTip = toolTip });
}

/// <summary>插件贡献的工具栏按钮描述。</summary>
public sealed class ToolbarButtonInfo
{
    /// <summary>按钮文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>点击回调（主程序保证在 UI 线程调用）。</summary>
    public Action? Clicked { get; set; }

    /// <summary>悬浮提示（可选）。</summary>
    public string? ToolTip { get; set; }
}