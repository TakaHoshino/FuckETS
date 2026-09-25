using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;
using FuckETS.PluginSdk.Packaging;
using FuckETS.Services;

namespace FuckETS.Plugins;

/// <summary>
/// 插件宿主：管理内置与外部 .fep 插件，提供钩子引擎（发布/订阅）与解析插件选择。
/// 单一实例，由应用启动时初始化。
/// </summary>
public sealed class PluginHost
{
    /// <summary>内置广东高中解析插件的稳定 id。</summary>
    public const string BuiltInGuangdongId = "fuckets.builtin.guangdong-highschool";

    private readonly HookEventSink _sink = new();
    private readonly List<PluginInfo> _plugins = new();
    private readonly Dictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
    private readonly Dictionary<string, PluginInfo> _byId = new();
    private PluginSettings _settings = new();
    private bool _initialized;

    /// <summary>插件变更后触发（用于 UI 刷新下拉框）。</summary>
    public event EventHandler? PluginsChanged;

    /// <summary>默认解析插件信息。</summary>
    public PluginInfo DefaultParser { get; private set; } = null!;

    public static PluginHost Current { get; private set; } = null!;

    public static void EnsureInitialized()
    {
        if (Current is null)
        {
            Current = new PluginHost();
            Current.Initialize();
        }
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _settings = PluginSettingsService.Load();

        RegisterBuiltInParser();

        // 扫描外部插件目录
        var pluginDir = GetExternalPluginDirectory();
        ScanPluginDirectory(pluginDir);

        Logger.Info($"插件宿主已初始化：内置 1，外部 {_plugins.Count(p => !p.BuiltIn)} 个。");
    }

    private void RegisterBuiltInParser()
    {
        try
        {
            var manifest = new PluginManifest
            {
                Id = BuiltInGuangdongId,
                Name = "广东高中试题解析",
                Version = "2.0.1",
                Author = "FuckETS",
                Type = PluginType.Parser,
                Region = "Guangdong-HighSchool",
                Assembly = "FuckETS.dll",
                EntryType = typeof(BuiltInGuangdongPlugin).FullName ?? "FuckETS.Plugins.BuiltInGuangdongPlugin",
                ApiVersion = PluginApiVersion.Current,
                BuiltIn = true,
                Description = "解析广东地区高中 E听说 试题（PartA 模仿朗读 / PartB 角色扮演 / PartC 故事复述）。",
            };

            var plugin = new BuiltInGuangdongPlugin();
            plugin.OnLoad();

            var info = new PluginInfo
            {
                Manifest = manifest,
                Instance = plugin,
                Source = "built-in",
                BuiltIn = true,
                Enabled = !IsDisabled(manifest.Id),
            };
            AddPlugin(info);
        }
        catch (Exception ex)
        {
            Logger.Error($"注册内置解析插件失败：{ex.Message}", ex);
        }
    }

    /// <summary>外部插件目录（程序目录 plugins\）。</summary>
    public static string GetExternalPluginDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "plugins");
    }

    private void ScanPluginDirectory(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Logger.Debug($"插件目录不存在，跳过：{dir}");
            return;
        }

        foreach (var fep in Directory.EnumerateFiles(dir, "*" + FepPackager.Extension, SearchOption.TopDirectoryOnly))
        {
            LoadExternalPlugin(fep);
        }
    }

    /// <summary>加载一个外部 .fep 插件。校验失败或异常会被隔离并记录。</summary>
    public PluginInfo? LoadExternalPlugin(string fepPath)
    {
        Logger.Debug($"尝试加载插件包：{fepPath}");
        var read = FepPackager.ReadManifest(fepPath);
        if (!read.Success || read.Manifest is null)
        {
            Logger.Warn($"插件加载被拒：{fepPath}（{read.Error}）。");
            return null;
        }

        // 已存在同一 id 则跳过（避免重复加载）
        if (_byId.ContainsKey(read.Manifest.Id))
        {
            Logger.Warn($"插件已存在，跳过：{read.Manifest.Id}（{fepPath}）。");
            return null;
        }

        try
        {
            // 解包到临时目录以便用 AssemblyLoadContext 加载程序集
            using var zip = System.IO.Compression.ZipFile.OpenRead(fepPath);
            var tempDir = Path.Combine(Path.GetTempPath(), "FuckETS.Plugins", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            zip.ExtractToDirectory(tempDir);

            var asmPath = Path.Combine(tempDir, read.Manifest.Assembly);
            if (!File.Exists(asmPath))
            {
                Logger.Warn($"插件包内缺少程序集：{read.Manifest.Assembly}（{fepPath}）。");
                return null;
            }

            var alc = new PluginAssemblyLoadContext(asmPath);
            var assembly = alc.LoadPluginAssembly(asmPath);
            var type = assembly.GetType(read.Manifest.EntryType, throwOnError: false);
            if (type is null)
            {
                Logger.Warn($"插件入口类型不存在：{read.Manifest.EntryType}（{fepPath}）。");
                return null;
            }
            if (!typeof(IPlugin).IsAssignableFrom(type))
            {
                Logger.Warn($"插件入口类型未实现 IPlugin：{type.FullName}（{fepPath}）。");
                return null;
            }

            var instance = (IPlugin)Activator.CreateInstance(type)!;
            instance.OnLoad();

            var info = new PluginInfo
            {
                Manifest = read.Manifest,
                Instance = instance,
                Source = fepPath,
                BuiltIn = false,
                Enabled = !IsDisabled(read.Manifest.Id),
            };
            _loadContexts[info.Manifest.Id] = alc;
            AddPlugin(info);
            if (info.Enabled)
                RegisterBehaviorHooks(info);
            Logger.Info($"插件加载成功：{info.DisplayName}（{fepPath}）。");
            return info;
        }
        catch (Exception ex)
        {
            Logger.Error($"插件加载异常：{fepPath}（{ex.Message}）。", ex);
            return null;
        }
    }

    private void AddPlugin(PluginInfo info)
    {
        _plugins.Add(info);
        _byId[info.Manifest.Id] = info;
    }

    private bool IsDisabled(string id) => _settings.Enabled.TryGetValue(id, out var disabled) && disabled;

    /// <summary>所有插件（含内置）。</summary>
    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    /// <summary>全部启用且类型为解析的插件（用于下拉框）。默认内置位首。</summary>
    public IReadOnlyList<PluginInfo> GetEnabledParserPlugins()
        => _plugins.Where(p => p.IsParser && p.Enabled)
            .OrderByDescending(p => p.BuiltIn)
            .ToList();

    /// <summary>设置某插件启用/禁用，并持久化。行为插件在启停时注册/移除钩子。</summary>
    public bool SetEnabled(string id, bool enabled)
    {
        if (!_byId.TryGetValue(id, out var info))
            return false;
        info.Enabled = enabled;
        _settings.Enabled[id] = !enabled; // 反向存储：true 表示禁用
        PluginSettingsService.Save(_settings);

        try
        {
            if (info.Instance is IBehaviorPlugin)
            {
                if (enabled)
                    RegisterBehaviorHooks(info);
                else
                    RemoveBehaviorHooks(info);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"更新插件 {id} 钩子订阅失败：{ex.Message}", ex);
        }

        PluginsChanged?.Invoke(this, EventArgs.Empty);
        Logger.Info($"插件启用状态更新：{id} → {(enabled ? "启用" : "禁用")}。");
        return true;
    }

    /// <summary>让行为插件注册钩子（仅启用时）。</summary>
    private void RegisterBehaviorHooks(PluginInfo info)
    {
        if (info.Instance is IBehaviorPlugin bp)
        {
            bp.RegisterHooks(_sink);
            Logger.Debug($"行为插件已注册钩子：{info.Manifest.Id}");
        }
    }

    /// <summary>移除行为插件注册的全部钩子（禁用/卸载时）。</summary>
    private void RemoveBehaviorHooks(PluginInfo info)
    {
        if (info.Instance is null)
            return;
        var removed = _sink.RemoveHandlersOwnedBy(info.Instance.GetType().Assembly);
        if (removed > 0)
            Logger.Debug($"行为插件已移除 {removed} 个钩子订阅：{info.Manifest.Id}");
    }

    /// <summary>移除一个外部插件：清理钩子、卸载加载上下文、删除 .fep 文件并持久化状态。内置插件不可移除。</summary>
    public bool RemoveExternalPlugin(string id)
    {
        if (!_byId.TryGetValue(id, out var info) || info.BuiltIn)
        {
            Logger.Warn($"无法移除插件：{id}（内置插件不可移除或不存在）。");
            return false;
        }

        _plugins.Remove(info);
        _byId.Remove(id);
        RemoveBehaviorHooks(info);

        if (_loadContexts.Remove(id, out var alc))
            alc.Unload();

        _settings.Enabled.Remove(id);
        if (_settings.SelectedParserId == id)
            _settings.SelectedParserId = null;
        PluginSettingsService.Save(_settings);

        // 删除插件包文件（临时解包目录留待系统清理）
        try
        {
            if (File.Exists(info.Source))
                File.Delete(info.Source);
        }
        catch (Exception ex)
        {
            Logger.Warn($"删除插件文件失败：{info.Source}（{ex.Message}）。");
        }

        PluginsChanged?.Invoke(this, EventArgs.Empty);
        Logger.Info($"插件已移除：{info.DisplayName}（{id}）。");
        return true;
    }

    /// <summary>当前选中的启用解析插件；选中项失效时回退默认内置插件。</summary>
    public PluginInfo? GetSelectedParser(out bool fellBack)
    {
        fellBack = false;
        var enabled = GetEnabledParserPlugins();
        if (enabled.Count == 0)
            return null;

        // 若当前配置选中且仍启用，返回之
        if (!string.IsNullOrEmpty(_settings.SelectedParserId))
        {
            var sel = enabled.FirstOrDefault(p => p.Manifest.Id == _settings.SelectedParserId);
            if (sel is not null)
                return sel;
            fellBack = true;
        }
        else
        {
            fellBack = _settings.SelectedParserId is not null;
        }

        // 回退：内置优先，否则第一个启用解析插件
        var fallback = enabled.FirstOrDefault(p => p.BuiltIn) ?? enabled[0];
        if (fallback is not null && _settings.SelectedParserId != fallback.Manifest.Id)
        {
            _settings.SelectedParserId = fallback.Manifest.Id;
            PluginSettingsService.Save(_settings);
        }
        return fallback;
    }

    /// <summary>设置选中的解析插件 id 并持久化。</summary>
    public void SelectParser(string? id)
    {
        _settings.SelectedParserId = id;
        PluginSettingsService.Save(_settings);
    }

    /// <summary>完整解析一次作业文件夹：枚举 content_* 子目录 → 调度解析插件（含识别/解析钩子）→ 解析完成钩子。</summary>
    public List<string> RunParse(PluginInfo parser, string folderPath)
    {
        var context = new ParseContext(folderPath) { ParserPluginId = parser.Manifest.Id };
        var output = context.OutputLines;

        var contentPattern = new Regex(@"^content_(\d+)$");
        var matches = new List<SubItem>();
        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(folderPath))
            {
                var m = contentPattern.Match(Path.GetFileName(dirPath));
                if (m.Success)
                    matches.Add(new SubItem(int.Parse(m.Groups[1].Value), dirPath));
            }
        }
        catch (Exception ex)
        {
            output.Add($"读取子文件夹出错：{ex.Message}");
            Logger.Error($"枚举 content 子目录失败：{ex.Message}", ex);
            return output;
        }

        context.ContentItems = matches;
        Logger.Debug($"解析：找到 {matches.Count} 个 content_* 子目录。");

        Parse(parser, context);

        // 解析完成钩子：插件可改写全部输出行
        var completed = new ParseCompletedContext();
        completed.OutputLines.AddRange(output);
        _sink.Publish(HookNames.ParseCompleted, this, completed, RecordHookError);
        if (completed.OutputLines.Count != output.Count ||
            !output.SequenceEqual(completed.OutputLines))
        {
            output.Clear();
            output.AddRange(completed.OutputLines);
        }

        return output;
    }

    /// <summary>调度指定解析插件执行解析（Part 识别前后钩子 + Part 解析前后钩子）。输出行追加至 context.OutputLines。</summary>
    private void Parse(PluginInfo parser, ParseContext context)
    {
        if (parser.Instance is not IParserPlugin pp)
        {
            Logger.Warn($"插件 {parser.Manifest.Id} 不是解析插件，无法执行解析。");
            return;
        }

        Logger.Debug($"使用解析插件 {parser.Manifest.Id} 执行解析：{context.FolderPath}");
        var output = context.OutputLines;

        // Part 识别前钩子（逐 content 目录）
        foreach (var item in context.ContentItems)
            _sink.Publish(HookNames.PartDetectBefore, this,
                new PartDetectBeforeContext { ContentDir = item.Path, ContentId = item.Id }, RecordHookError);

        var detected = pp.DetectParts(context);

        // content 项 → Part 映射（插件识别结果）
        var itemParts = new Dictionary<SubItem, string>();
        foreach (var (part, items) in detected)
            foreach (var it in items)
                itemParts[it] = part;

        // Part 识别后钩子（可改写单个 content 项的 Part 归属）
        var postParts = new Dictionary<SubItem, string>();
        foreach (var item in context.ContentItems)
        {
            itemParts.TryGetValue(item, out var part);
            var afterDetect = new PartDetectAfterContext { ContentDir = item.Path, Part = part };
            _sink.Publish(HookNames.PartDetectAfter, this, afterDetect, RecordHookError);
            if (!string.IsNullOrEmpty(afterDetect.Part))
                postParts[item] = afterDetect.Part!;
        }

        // 每个 Part 取 Id 最大（最新）的命中目录
        var chosen = new Dictionary<string, SubItem>();
        foreach (var (item, part) in postParts)
        {
            if (!chosen.TryGetValue(part, out var best) || item.Id >= best.Id)
                chosen[part] = item;
        }

        // 稳定输出顺序：插件识别的 Part 键序（有序）优先，钩子引入的新键随后
        var order = new List<string>(detected.Keys);
        foreach (var part in chosen.Keys)
            if (!order.Contains(part))
                order.Add(part);

        foreach (var part in order)
        {
            if (!chosen.TryGetValue(part, out var sub))
            {
                // 未识别到对应子文件夹（与既有行为一致）
                output.Add($"\n【{part}】 未找到对应的子文件夹");
                Logger.Warn($"解析：未识别到 {part} 对应的子文件夹。");
                continue;
            }

            var label = pp.GetPartLabel(part);

            // Part 解析前钩子（Cancel = 跳过该 Part，不输出任何行）
            var before = new PartParseBeforeContext
            {
                Part = part,
                PartLabel = label,
                JsonPath = Path.Combine(sub.Path, "content.json"),
            };
            _sink.Publish(HookNames.PartParseBefore, this, before, RecordHookError);
            if (before.Cancel)
            {
                Logger.Debug($"Part 解析被插件取消：{part}（{before.JsonPath}）。");
                continue;
            }

            output.Add($"\n【{part}】 {label}");

            var produced = pp.ParsePart(context, part, before.JsonPath);
            foreach (var line in produced)
                output.Add(line);
            Logger.Debug($"PartDetector：{Path.GetFileName(sub.Path)} → {part}");

            // Part 解析后钩子（可读取该 Part 的产出行）
            var after = new PartParseAfterContext { Part = part, PartLabel = label, JsonPath = before.JsonPath };
            after.ProducedLines.AddRange(produced);
            _sink.Publish(HookNames.PartParseAfter, this, after, RecordHookError);
        }
    }

    /// <summary>发布一个钩子事件。</summary>
    public void Publish(string hookName, HookContext context)
    {
        _sink.Publish(hookName, this, context, RecordHookError);
    }

    private void RecordHookError(Exception ex)
    {
        Logger.Error($"插件钩子调用异常：{ex.Message}", ex);
    }

    /// <summary>供行为插件在内部使用的事件目录（由宿主在加载时传入）。</summary>
    public HookEventSink Events => _sink;
}