namespace FuckETS.Models;

/// <summary>扫描到的作业文件夹条目。</summary>
public sealed record FolderItem(string Name, string Path, string CreationTime)
{
    /// <summary>作业标题（从 ETS 日志解析，可为空）。</summary>
    public string HomeworkTitle { get; set; } = string.Empty;
}