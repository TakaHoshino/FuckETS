using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;

namespace HelloBehaviorPlugin;

/// <summary>
/// 示例行为插件：演示如何订阅钩子。
/// 启用后，每次解析会在结果末尾追加一行标记（禁用即消失），便于直观验证钩子生效。
/// </summary>
public sealed class HelloBehavior : IBehaviorPlugin
{
    public PluginManifest GetManifest() => new()
    {
        Id = "example.hello-behavior",
        Name = "示例·行为插件",
        Version = "1.0.0",
        Author = "FuckETS 示例",
        Type = PluginType.Behavior,
        Assembly = "HelloBehaviorPlugin.dll",
        EntryType = typeof(HelloBehavior).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "演示钩子：解析完成时在输出末尾追加一行标记。",
    };

    public void OnLoad() { }

    public void RegisterHooks(HookEventSink events)
    {
        // 解析完成钩子：追加演示标记行（priority 越小越先执行）
        events.Subscribe(HookNames.ParseCompleted, priority: 100, (_, e) =>
        {
            if (e is ParseCompletedContext ctx)
                ctx.OutputLines.Add("[示例插件] 本次解析经过了 parse.completed 钩子。");
        });

        // 扫描完成钩子：仅记录（可在此修改文件夹列表）
        events.Subscribe(HookNames.ScanCompleted, priority: 100, (_, e) =>
        {
            if (e is ScanCompletedContext ctx)
                System.Diagnostics.Debug.WriteLine($"[示例插件] 扫描到 {ctx.Folders.Count} 个作业文件夹。");
        });
    }
}