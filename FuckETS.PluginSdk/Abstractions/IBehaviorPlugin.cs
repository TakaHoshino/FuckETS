using FuckETS.PluginSdk.Context;

namespace FuckETS.PluginSdk.Abstractions;

/// <summary>
/// 行为插件接机：扩展软件行为。通过 <c>RegisterHooks</c> 向宿主注册对钩子点的处理。
/// </summary>
public interface IBehaviorPlugin : IPlugin
{
    /// <summary>
    /// 注册本插件关心的钩子。方法接收事件目录，事件目录提供按 <see cref="HookNames"/> 名称订阅的能力。
    /// 订阅的回调签名：<c>void Handler(object? sender, HookContext e)</c>。
    /// </summary>
    /// <param name="events">钩子目录（事件聚合），用于订阅各钩子点。</param>
    void RegisterHooks(HookEventSink events);
}