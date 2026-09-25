using FuckETS.PluginSdk.Models;

namespace FuckETS.PluginSdk.Abstractions;

/// <summary>
/// 所有插件的基接口。插件仅依赖 PluginSdk，不直接引用主程序内部类型。
/// </summary>
public interface IPlugin
{
    /// <summary>返回插件清单元数据（名称/版本/作者/类型/地区标识等）。</summary>
    PluginManifest GetManifest();

    /// <summary>初始化插件实例。仅在任何钩子调用前调用一次。抛出异常会终止该插件的加载。</summary>
    void OnLoad();
}