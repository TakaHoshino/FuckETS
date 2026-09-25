using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;

namespace FuckETS.PluginSdk.Abstractions;

/// <summary>钩子回调签名。sender 为主程序事件源，e 为各钩子对应的上下文。</summary>
public delegate void HookHandler(object? sender, HookContext e);

/// <summary>
/// 钩子事件目录。行为插件在其 <see cref="IBehaviorPlugin.RegisterHooks"/> 中通过
/// <c>Subscribe(name, priority, handler)</c> 订阅钩子。实现在主程序宿主中，插件侧仅依赖此契约。
/// </summary>
public sealed class HookEventSink
{
    private readonly Dictionary<string, List<(int Priority, HookHandler Handler)>> _handlers = new();
    private readonly object _lock = new();

    /// <summary>订阅指定钩子。priority 越小越先执行（支持优先级链）。</summary>
    /// <returns>一个可调用以注销本次订阅的句柄。</returns>
    public IDisposable Subscribe(string hookName, int priority, HookHandler handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(hookName, out var list))
            {
                list = new List<(int, HookHandler)>();
                _handlers[hookName] = list;
            }
            list.Add((priority, handler));
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }

        return new Subscription(this, hookName, handler);
    }

    /// <summary>针对指定钩子，依优先级依次调用已订阅处理器，并对单个处理器异常做隔离（否决并记录）。</summary>
    public void Publish(string hookName, object? sender, HookContext e, Action<Exception> onError)
    {
        (int, HookHandler)[] snapshot;
        lock (_lock)
        {
            snapshot = _handlers.TryGetValue(hookName, out var list) ? list.ToArray() : Array.Empty<(int, HookHandler)>();
        }

        foreach (var (_, handler) in snapshot)
        {
            try
            {
                handler(sender, e);
            }
            catch (Exception ex)
            {
                // 某插件异常不得导致主流程崩溃：隔离并记录。
                onError(ex);
            }
        }
    }

    /// <summary>注销一个订阅。</summary>
    internal void Unsubscribe(string hookName, HookHandler handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(hookName, out var list))
                list.RemoveAll(h => h.Item2 == handler);
        }
    }

    /// <summary>移除某程序集（插件）注册的全部处理器，返回移除数量。用于插件禁用/卸载时清理。</summary>
    public int RemoveHandlersOwnedBy(System.Reflection.Assembly assembly)
    {
        int removed = 0;
        lock (_lock)
        {
            foreach (var list in _handlers.Values)
                removed += list.RemoveAll(h => h.Handler.Method.DeclaringType?.Assembly == assembly);
        }
        return removed;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly HookEventSink _sink;
        private readonly string _hookName;
        private readonly HookHandler _handler;
        private bool _disposed;

        public Subscription(HookEventSink sink, string hookName, HookHandler handler)
        {
            _sink = sink;
            _hookName = hookName;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sink.Unsubscribe(_hookName, _handler);
        }
    }
}