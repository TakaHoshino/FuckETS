using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;

namespace ToolbarDemoPlugin;

/// <summary>
/// 示例行为插件：在主界面工具栏显示「示例插件」按钮，
/// 点击后弹出信息框（标题「示例插件」、内容「FuckETS 示例行为插件」）。
/// 演示 ui.mainWindow.toolbar 钩子——插件无需依赖 WPF 即可扩展主界面。
/// </summary>
public sealed class ToolbarDemo : IBehaviorPlugin
{
    public PluginManifest GetManifest() => new()
    {
        Id = "example.toolbar-demo",
        Name = "示例·工具栏插件",
        Version = "1.0.0",
        Author = "FuckETS 示例",
        Type = PluginType.Behavior,
        Assembly = "ToolbarDemoPlugin.dll",
        EntryType = typeof(ToolbarDemo).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "主界面显示「示例插件」按钮，点击弹出信息框。",
    };

    public void OnLoad() { }

    public void RegisterHooks(HookEventSink events)
    {
        events.Subscribe(HookNames.MainWindowToolbar, priority: 100, (_, e) =>
        {
            if (e is not MainWindowToolbarContext ctx)
                return;

            ctx.AddButton(
                text: "示例插件",
                onClick: () => ctx.ShowMessage?.Invoke("示例插件", "FuckETS 示例行为插件"),
                toolTip: "示例行为插件：点击弹出信息框");
        });
    }
}