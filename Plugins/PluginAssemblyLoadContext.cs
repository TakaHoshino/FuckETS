using System.Reflection;
using System.Runtime.Loader;
using FuckETS.PluginSdk;

namespace FuckETS.Plugins;

/// <summary>
/// 为 .fep 插件程序集提供隔离的加载上下文（可卸载、依赖探测），避免与主程序程序集冲突。
/// </summary>
internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginAssemblyLoadContext(string pluginAssemblyPath)
        : base(name: $"FuckETS.Plugin-{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 插件可能依赖 SDK：若与主程序加载的 SDK 同一标识，则复用主程序已加载的程序集。
        if (assemblyName.Name == typeof(FuckETS.PluginSdk.Abstractions.IPlugin).Assembly.GetName().Name)
            return typeof(FuckETS.PluginSdk.Abstractions.IPlugin).Assembly;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }

    /// <summary>加载插件入口程序集。</summary>
    public Assembly LoadPluginAssembly(string assemblyPath) => LoadFromAssemblyPath(assemblyPath);
}