namespace FuckETS.PluginSdk.Context;

/// <summary>
/// 一次试题解析的上下文对象。贯穿「扫描 → Part 识别 → Part 解析 → 完成」整个解析流程。
/// 上下文可读写：插件可持续写入解析结果行；主程序在各钩子间传递同一实例。
/// </summary>
public sealed class ParseContext
{
    /// <summary>当前被解析的作业文件夹路径。</summary>
    public string FolderPath { get; }

    /// <summary>已产出/可修改的解析结果文本行（含换行符由调用方处理，行本身不含换行）。</summary>
    public List<string> OutputLines { get; } = new();

    /// <summary>扫描到的 content_&lt;数字&gt; 子目录及其编号（Id, 绝对路径）。供 Part 识别与解析使用。</summary>
    public List<SubItem> ContentItems { get; set; } = new();

    /// <summary>任意插件/宿主可写入的共享数据字典（Key-Value），用于跨钩子传递自定义数据。</summary>
    public Dictionary<string, object?> Shared { get; } = new();

    /// <summary>标识当前使用哪个解析插件（Info 由主程序注入）。</summary>
    public string? ParserPluginId { get; set; }

    public ParseContext(string folderPath)
    {
        FolderPath = folderPath;
    }
}

/// <summary>一个 content_&lt;数字&gt; 子目录项。</summary>
public readonly record struct SubItem(int Id, string Path);