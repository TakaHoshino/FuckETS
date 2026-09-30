# FuckETS 插件开发指南（Plugin SDK 1.0）

本指南面向想要为 FuckETS 编写插件的开发者。阅读后你将能够：

- 理解 FuckETS 的插件架构与加载模型
- 编写、打包并安装一个**行为插件**或**解析插件**
- 使用钩子（Hook）干预扫描、解析、展示、导出等全流程
- 排查插件加载与运行中的常见问题

---

## 目录

1. [插件体系概览](#1-插件体系概览)
2. [架构与加载模型](#2-架构与加载模型)
3. [快速开始](#3-快速开始)
4. [plugin.json 清单详解](#4-pluginjson-清单详解)
5. [插件生命周期](#5-插件生命周期)
6. [接口契约](#6-接口契约)
7. [钩子机制详解](#7-钩子机制详解)
8. [钩子目录与上下文](#8-钩子目录与上下文)
9. [解析流程时序（IParserPlugin 深入）](#9-解析流程时序iparserplugin-深入)
10. [输出行约定（PDF / 高亮依赖）](#10-输出行约定pdf--高亮依赖)
11. [.fep 包格式与打包](#11-fep-包格式与打包)
12. [程序集隔离与依赖](#12-程序集隔离与依赖)
13. [启用状态与持久化](#13-启用状态与持久化)
14. [调试与排错](#14-调试与排错)
15. [线程模型与安全须知](#15-线程模型与安全须知)
16. [完整示例骨架](#16-完整示例骨架)
17. [最佳实践与 FAQ](#17-最佳实践与-faq)
18. [附录：SDK 类型速查](#18-附录sdk-类型速查)

---

## 1. 插件体系概览

FuckETS 把「试题格式解析」和「功能扩展」都交给插件完成，主程序只做**宿主与编排**。插件分两类：

| 类型 | 接口 | 运行方式 | 典型用途 |
| --- | --- | --- | --- |
| **行为插件**（`behavior`） | `IBehaviorPlugin` | 被动：订阅钩子，在钩子被触发时执行 | 改写解析结果、加工展示、自定义导出、注入界面按钮 |
| **解析插件**（`parser`） | `IParserPlugin` | 主动：被宿主直接调用 | 适配特定地区/实体的题型识别与答案解析 |

一个插件**只能**是其中一种（由清单 `type` 字段决定）。

所有插件契约都位于类库 **`FuckETS.PluginSdk`**。核心原则：

> **插件只依赖 `FuckETS.PluginSdk`，绝不引用主程序内部类型。**

这带来两个好处：插件可以用普通类库项目开发（甚至不需要 WPF），并且可以随主程序版本演进保持稳定。

---

## 2. 架构与加载模型

```
┌─────────────────────────────────────────────────────────┐
│                     FuckETS（主程序）                     │
│  PluginHost ── 钩子引擎 HookEventSink ── 解析调度 RunParse │
│      │                        ▲                          │
│      │ 加载/启用/禁用          │ 订阅(Publish/Subscribe)   │
└──────┼────────────────────────┼──────────────────────────┘
       │                        │
       ▼                        │
┌──────────────┐        ┌───────┴────────┐
│ 内置解析插件   │        │  外部 .fep 插件  │
│ (随程序分发)   │        │ (AssemblyLoadCtx)│
└──────────────┘        └────────────────┘
       └────────── 都只依赖 ──────────────┘
                    FuckETS.PluginSdk
```

**加载时机**：应用启动时 `App.OnStartup` → `PluginHost.EnsureInitialized()`，此时：

1. 注册**内置「广东高中」解析插件**（id 固定为 `fuckets.builtin.guangdong-highschool`）
2. 扫描程序目录下的 `plugins\*.fep` 并逐个加载

**外部插件目录**：`{程序目录}\plugins\`（`PluginHost.GetExternalPluginDirectory()`）。

启动后还可在主界面「**插件管理…**」中动态加载/启停/移除插件。

---

## 3. 快速开始

### 3.1 新建插件项目

新建一个 `net8.0` 类库项目（**不需要** WPF），引用仓库内 `FuckETS.PluginSdk`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>MyPlugin</RootNamespace>
    <AssemblyName>MyPlugin</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\FuckETS.PluginSdk\FuckETS.PluginSdk.csproj" />
  </ItemGroup>

  <!-- 让 plugin.json 随构建输出到 bin，便于打包 -->
  <ItemGroup>
    <None Include="plugin.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

### 3.2 编写清单 `plugin.json`

在项目根目录创建 `plugin.json`（字段详解见[第 4 节](#4-pluginjson-清单详解)）：

```json
{
  "id": "com.example.myplugin",
  "name": "我的插件",
  "version": "1.0.0",
  "author": "你的名字",
  "type": "behavior",
  "assembly": "MyPlugin.dll",
  "entryType": "MyPlugin.MyBehavior",
  "apiVersion": "1.0",
  "description": "一句话说明这个插件做什么。"
}
```

### 3.3 实现接口

```csharp
using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;

namespace MyPlugin;

public sealed class MyBehavior : IBehaviorPlugin
{
    public PluginManifest GetManifest() => new()
    {
        Id = "com.example.myplugin",
        Name = "我的插件",
        Version = "1.0.0",
        Author = "你的名字",
        Type = PluginType.Behavior,
        Assembly = "MyPlugin.dll",
        EntryType = typeof(MyBehavior).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "一句话说明。",
    };

    public void OnLoad() { }

    public void RegisterHooks(HookEventSink events)
    {
        events.Subscribe(HookNames.ParseCompleted, priority: 100, (_, e) =>
        {
            if (e is ParseCompletedContext ctx)
                ctx.OutputLines.Add("—— 由我的插件追加 ——");
        });
    }
}
```

> **注意**：`GetManifest()` 返回的清单应与 `plugin.json` 保持一致（id、type、assembly、entryType 等关键字段尤其重要）。

### 3.4 打包为 `.fep`

在仓库根目录执行：

```powershell
scripts\pack_fep.ps1 -ProjectDir path\to\MyPlugin
```

脚本会先 `dotnet build -c Release`，再把输出目录里的 `plugin.json` 与清单 `assembly` 指定的 DLL 打成 `<插件id>.fep`。

> 位于 `examples\` 下的插件项目**无需手动执行**：构建时会通过 `examples\Directory.Build.targets` 自动把 `.fep` 打包到项目自身输出目录 `bin\<Config>\net8.0\`。

### 3.5 安装并验证

- 主界面「**插件管理…**」→「**加载 .fep 插件…**」；或
- 把 `.fep` 复制到程序目录 `plugins\` 后重启应用。

行为插件启用后立即生效；解析插件会出现在主界面「**解析插件**」下拉框中供切换。

可参考完整示例：`examples/HelloBehaviorPlugin`、`examples/ToolbarDemoPlugin`（行为）、`examples/DemoParserPlugin`（解析）。

---

## 4. plugin.json 清单详解

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | ✔ | **全局唯一**（建议反向域名，如 `com.example.myplugin`）。重复 id 的插件会被拒绝加载 |
| `name` | string | ✔ | 显示名称 |
| `version` | string | | 语义化版本（默认 `1.0.0`），展示用 |
| `author` | string | | 作者，展示用 |
| `type` | string | ✔ | `behavior` 或 `parser`。**必须显式存在且为字符串**，缺失或非法即拒绝加载 |
| `region` | string | 解析插件建议 | 地区/实体标识，如 `Guangdong-HighSchool`。用于解析插件匹配与展示 |
| `assembly` | string | ✔ | `.fep` 包内**入口程序集文件名**（如 `MyPlugin.dll`），加载时据此定位 |
| `entryType` | string | ✔ | 入口类型**完全限定名**（如 `MyPlugin.MyBehavior`），须实现 `IPlugin` |
| `apiVersion` | string | ✔ | 要求的 API 版本，主版本须与 `PluginApiVersion.Current`（当前 `1.0`）一致 |
| `builtIn` | bool | | 内置标记，**对 `.fep` 外部插件无效**（由主程序内置注册时置位） |
| `description` | string | | 描述 |

### 校验规则（`ManifestValidator`）

加载前会依次校验：

1. `plugin.json` 能被解析为 JSON 对象，且 `type` 字段**存在且为字符串**（避免枚举默认值掩盖缺失）
2. `id`、`name`、`assembly`、`entryType`、`apiVersion` 非空
3. `type` 属于 `behavior` / `parser`
4. **API 版本兼容**：`apiVersion` 的**主版本**必须等于当前 SDK 主版本（`1.x` 与 `1.y` 兼容；主版本不同则拒绝）

任一步失败，插件被拒绝加载，原因写入日志 `%APPDATA%\FuckETS\logs\`。

### API 版本约定

`PluginApiVersion.Current` 当前为 `"1.0"`。主程序**只比较主版本号**：

```csharp
// 兼容：插件 1.0 / 1.1 / 1.9 均可加载到主程序 1.0
// 不兼容：插件 2.0 无法加载到主程序 1.0
```

这意味着 SDK 的 1.x 系列内会保持契约向后兼容；发生破坏性变更时才会提升主版本。

---

## 5. 插件生命周期

```
① 发现    扫描 plugins\*.fep，或用户通过「插件管理」选择 .fep
② 校验    FepPackager.ReadManifest：扩展名 → 有 plugin.json → 清单字段合法 → 包内含 assembly
③ 去重    若 id 已加载，跳过
④ 解包    解压到 %TEMP%\FuckETS.Plugins\{guid}\
⑤ 加载    在隔离的 AssemblyLoadContext 中加载 assembly，定位 entryType
⑥ 实例化  entryType 必须实现 IPlugin → Activator.CreateInstance
⑦ OnLoad  调用一次（异常会终止该插件加载）
⑧ 注册钩子 若为**行为插件**且当前为**启用**状态 → 调用 RegisterHooks
⑨ 就绪    加入插件列表，触发 PluginsChanged（UI 刷新）
```

**启停与卸载**：

- **禁用**行为插件：自动注销该插件程序集注册的**全部**钩子订阅
- **重新启用**：再次调用 `RegisterHooks`
- **移除**外部插件：注销钩子 → `Unload()` 加载上下文 → 清配置 → **删除 `.fep` 文件**
- **内置插件不可移除**，但可禁用

---

## 6. 接口契约

### 6.1 `IPlugin`（所有插件的基接口）

```csharp
public interface IPlugin
{
    /// <summary>返回插件清单元数据。</summary>
    PluginManifest GetManifest();

    /// <summary>初始化插件实例，仅在任何钩子调用前调用一次。</summary>
    void OnLoad();
}
```

- `GetManifest()` 在加载时调用，用于构建运行时信息。
- `OnLoad()` 抛异常会**终止该插件的加载**，但不会影响其他插件或主程序。
- **不要**在 `OnLoad()` 中注册钩子——行为插件的钩子注册请放在 `RegisterHooks()`。

### 6.2 `IBehaviorPlugin`（行为插件）

```csharp
public interface IBehaviorPlugin : IPlugin
{
    /// <summary>注册本插件关心的钩子（仅启用时被调用）。</summary>
    void RegisterHooks(HookEventSink events);
}
```

```csharp
public delegate void HookHandler(object? sender, HookContext e);
```

要点：

- 主程序在插件**启用时**调用 `RegisterHooks`；禁用/移除时自动注销该插件程序集注册的全部订阅。
- **只在 `RegisterHooks` 内订阅钩子。**
- 订阅时可指定 `priority`（越小越先执行）；同一钩子内的多个处理器按优先级链依次调用。
- 处理器抛出的异常会被**隔离并记录日志**，不会中断主流程。

### 6.3 `IParserPlugin`（解析插件）

```csharp
public interface IParserPlugin : IPlugin
{
    /// <summary>声明支持的地区/实体标识（如 "Guangdong-HighSchool"）。</summary>
    string RegionId { get; }

    /// <summary>
    /// 识别每个 content_* 子目录属于哪个 Part。
    /// 返回「Part 键 → 该 Part 命中的所有 content 项」。
    /// </summary>
    SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context);

    /// <summary>返回某个 Part 键的人类可读标签（如 "模仿朗读"）。</summary>
    string GetPartLabel(string part);

    /// <summary>解析某个 Part 对应 content.json 的正文，返回最终输出行（不含【Part】标题）。</summary>
    IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath);
}
```

约定：

- `DetectParts` 返回类型是 `SortedDictionary`，其**键序决定输出顺序**；应为你支持的**每个 Part 键**都返回条目（哪怕列表为空），以保证「未找到对应的子文件夹」提示按预期输出。
- `ParsePart` **只返回正文行**，`【PartX】 标签` 标题行由主程序拼接。
- 无法识别的 content 项不要放进任何键。

> **备注**：示例插件里还定义了 `public string? EntityId => null;`，但当前 `IParserPlugin` 接口并未声明该成员，属于预留约定，实现与否都不影响加载。

---

## 7. 钩子机制详解

钩子引擎由 `HookEventSink` 实现（定义在 SDK 中，主程序创建实例并传给插件的 `RegisterHooks`）。

### 7.1 订阅

```csharp
IDisposable subscription = events.Subscribe(
    hookName: HookNames.ParseCompleted,
    priority: 100,                       // 越小越先执行
    handler: (sender, e) =>
    {
        if (e is ParseCompletedContext ctx)
            ctx.OutputLines.Add("追加一行");
    });
```

- `Subscribe` 返回一个 `IDisposable`，调用其 `Dispose()` 可单独注销本次订阅。
- 订阅后宿主会保持列表按 `priority` 升序排序。
- 你无需手动管理注销：禁用/移除插件时，主程序会按程序集批量清理。

### 7.2 发布（宿主职责）

钩子由主程序在流程节点调用 `Publish` 触发。**插件不应主动 `Publish`。**

```csharp
// 主程序内部示意
_sink.Publish(hookName, sender: this, context, onError: RecordHookError);
```

`Publish` 的行为：

1. **加锁取快照**后按优先级顺序调用处理器（避免回调中修改订阅造成并发问题）
2. 单个处理器抛异常 → 交 `onError` 记录，**继续执行后续处理器**

### 7.3 优先级

`priority` 是整数，**数值越小越先执行**。例如：

- `priority: 0` → 最先（适合做预处理、规范化）
- `priority: 100` → 默认区间（适合普通加工）
- `priority: 1000` → 靠后（适合做收尾）

---

## 8. 钩子目录与上下文

`HookNames` 定义了全部可用钩子点：

| 钩子名（常量） | 值 | 上下文类型 | 时机 | 可修改内容 |
| --- | --- | --- | --- | --- |
| `ScanCompleted` | `scan.completed` | `ScanCompletedContext` | 扫描 + 作业标题加载后 | `Folders` 可增删改（路径无效的条目会被丢弃） |
| `FolderListLoaded` | `ui.folderList.loaded` | `FolderListLoadedContext` | 列表绑定到界面后 | 通知型（只读） |
| `PartDetectBefore` | `part.detect.before` | `PartDetectBeforeContext` | 单个 content 目录识别前 | 只读 |
| `PartDetectAfter` | `part.detect.after` | `PartDetectAfterContext` | 识别后 | 可改 `Part` 归属 |
| `PartParseBefore` | `part.parse.before` | `PartParseBeforeContext` | 某个 Part 解析前 | 只可设 `Cancel`（跳过该 Part） |
| `PartParseAfter` | `part.parse.after` | `PartParseAfterContext` | 某个 Part 解析后 | 可读 `ProducedLines` |
| `ParseCompleted` | `parse.completed` | `ParseCompletedContext` | 全部解析完成后 | 可改写 `OutputLines` |
| `ResultDisplaying` | `ui.result.displaying` | `ParseResultDisplayingContext` | 结果展示前 | 可改写展示行 |
| `PdfExporting` | `ui.pdf.exporting` | `PdfExportingContext` | PDF 导出前 | 可改 `OutputText` / `FileName` / `Options` |
| `MainWindowToolbar` | `ui.mainWindow.toolbar` | `MainWindowToolbarContext` | 主界面工具栏构建时 | 追加按钮；插件启停时会重新投递以重建按钮 |

所有上下文均继承自 `HookContext`，带有可读写的 `Cancel` 标志：

```csharp
public abstract class HookContext
{
    public bool Cancel { get; set; }
}
```

### 8.1 各上下文可读写字段一览

| 上下文 | 字段 | 可变性 | 说明 |
| --- | --- | --- | --- |
| `ScanCompletedContext` | `Folders : List<FolderItemInfo>` | 集合可增删改 | |
| `FolderListLoadedContext` | `Folders : List<FolderItemInfo>` | 集合可读写（无实际作用） | 通知型 |
| `PartDetectBeforeContext` | `ContentDir : string`<br>`ContentId : int` | init（只读） | |
| `PartDetectAfterContext` | `ContentDir : string`<br>`Part : string?`<br>`Signals : List<string>` | `Part` 可改 | `Signals` 供记录判定依据 |
| `PartParseBeforeContext` | `Part / PartLabel / JsonPath : string` | init（只读） | 仅 `Cancel` 可用 |
| `PartParseAfterContext` | `Part / PartLabel / JsonPath : string`<br>`ProducedLines : List<string>` | 只读 | `ProducedLines` 为该 Part 产出行 |
| `ParseCompletedContext` | `OutputLines : List<string>` | 集合可改 | 修改后主程序会用其结果替换输出 |
| `ParseResultDisplayingContext` | `OutputLines : List<string>` | 集合可改 | 仅影响界面展示 |
| `PdfExportingContext` | `OutputText / FileName : string`<br>`Options : IList<PdfExportOption>` | 可改 | |
| `MainWindowToolbarContext` | `Buttons : IReadOnlyList<ToolbarButtonInfo>`<br>`ShowMessage : Action<string,string>?` | 通过 `AddButton` 追加 | |

> **重要（与旧版文档的差异）**：`PartParseBeforeContext` 的 `JsonPath` 当前是 `init` 只读的，**插件无法在 `part.parse.before` 中替换 JSON 路径**，只能通过 `Cancel = true` 跳过该 Part。若需对某些 Part 做后处理，请使用 `part.parse.after` 或 `parse.completed`。

### 8.2 PDF 导出参数键

`PdfExportingContext.Options` 在投递前由主程序预置以下键，插件可读取或覆盖：

| Key | 类型 | 含义 | 合法范围 |
| --- | --- | --- | --- |
| `fontSize` | `float` | 正文字号（pt） | 8 – 72 |
| `titleFontSize` | `float` | 作业标题字号（pt） | 10 – 96 |
| `partHeadingFontSize` | `float` | Part 标题字号（pt） | 10 – 72 |
| `marginMm` | `float` | 页边距（mm） | 5 – 50 |
| `lineHeight` | `float` | 行间距倍率 | 1 – 3 |
| `boldHeadings` | `bool` | 是否粗体标题 | — |

### 8.3 主界面工具栏扩展

`MainWindowToolbarContext` 让插件**无需依赖 WPF** 就能扩展主界面：

```csharp
events.Subscribe(HookNames.MainWindowToolbar, priority: 100, (_, e) =>
{
    if (e is not MainWindowToolbarContext ctx) return;

    ctx.AddButton(
        text: "我的按钮",
        onClick: () => ctx.ShowMessage?.Invoke("标题", "内容"),
        toolTip: "悬浮提示（可选）");
});
```

- `AddButton(onClick)` 的回调由主程序保证在 **UI 线程**执行。
- `ShowMessage` 由主程序在投递钩子前注入，可在按钮回调中用于弹出信息框。
- 插件启停时会重新投递该钩子，按钮随之重建。

---

## 9. 解析流程时序（IParserPlugin 深入）

当用户点击「解析所选文件夹」，主程序 `PluginHost.RunParse(parser, folderPath)` 按以下顺序执行：

```
① 枚举作业目录下的 content_<数字> 子目录
      → context.ContentItems = [SubItem(Id, Path), ...]

② 对每个 content 目录投递 part.detect.before（只读）

③ parser.DetectParts(context)
      → SortedDictionary<Part, List<SubItem>>

④ 对每个 content 目录投递 part.detect.after
      → 插件可改写该目录的 Part 归属
      → 每个 Part 只保留「Id 最大（最新）」的命中目录

⑤ 计算输出顺序：插件 DetectParts 返回的 Part 键序优先，
   钩子新引入的 Part 键追加在后

⑥ 对每个 Part：
      a. 若该 Part 无命中目录 → 输出 "\n【PartX】 未找到对应的子文件夹"
      b. 投递 part.parse.before（可 Cancel 跳过该 Part）
      c. 输出标题行 "\n【PartX】 {GetPartLabel(part)}"
      d. produced = parser.ParsePart(context, part, jsonPath)
      e. 逐行追加 produced
      f. 投递 part.parse.after（可读取 ProducedLines）

⑦ 投递 parse.completed（可整体改写 OutputLines）
```

**关键点**：

- **同 Part 多目录取最新**：若同一 Part 命中了多个 `content_*` 目录，只解析 `Id` 最大的那个。
- **标题行由宿主拼接**：`ParsePart` 不要输出 `【PartX】`。
- **稳定顺序**：用 `SortedDictionary`（如 `StringComparer.Ordinal`）保证键序稳定。
- **`SubItem` 是值类型 `readonly record struct`**：按值相等，可直接作字典键。

### 9.1 `ParseContext` 说明

```csharp
public sealed class ParseContext
{
    public string FolderPath { get; }                    // 当前作业文件夹路径
    public List<string> OutputLines { get; }             // 解析结果行（可写入）
    public List<SubItem> ContentItems { get; set; }      // content_* 子目录
    public Dictionary<string, object?> Shared { get; }   // 跨钩子共享数据
    public string? ParserPluginId { get; set; }          // 当前解析插件 id
}
```

- `Shared` 是一个跨钩子传递自定义数据的字典，便于行为插件之间协作。
- 所有钩子共享**同一个 `ParseContext` 实例**（解析流程内）。

---

## 10. 输出行约定（PDF / 高亮依赖）

结果的着色与 PDF 排版依赖 `LineClassifier` 对行首特征的识别。为保证你产出的行被正确高亮/排版，请遵循：

| 行类型 | 格式 | 被识别为 |
| --- | --- | --- |
| Part 标题 | `【PartA】 模仿朗读`（由宿主输出） | PartHeading |
| 问题行 | `【问题 1】 问题文本` | Question |
| 候选答案引导 | `  候选答案：` | AnswerCandidate |
| 答案行 | `    1. 答案文本`（续行缩进 7 空格） | AnswerCandidate |
| 信息/提示 | `[完成]…`、`警告：…`、`错误：…` | Info |
| 空白行 | （空字符串） | 用于段落间距 |

**要求**：

- `ParsePart` 产出**正文行**，不要包含 `【PartX】` 标题。
- **不要**输出文件夹名、检测信号、完成时间等无关元数据（会污染结果）。
- 错误行建议以 `错误：` 或 `[PartX 解析错误]` 开头，便于识别为 Info。
- 多行答案的续行请保持缩进（如 7 空格），保持排版整齐。

---

## 11. .fep 包格式与打包

### 11.1 包结构

`.fep` 是一个 ZIP 压缩包，**根目录**包含：

```
your-plugin.fep
├── plugin.json          # 清单（固定文件名）
└── YourPlugin.dll       # 入口程序集（由清单 assembly 字段指定）
```

### 11.2 `FepPackager` API

```csharp
public static class FepPackager
{
    public const string ManifestFileName = "plugin.json";
    public const string Extension = ".fep";

    // 读取并校验清单（扩展名/清单/程序集是否存在）
    public static FepReadResult ReadManifest(string fepPath);

    // 从目录打包：sourceDir 下需有 plugin.json 与清单指定的程序集
    public static void Create(string sourceDir, string manifestFileName, string outputPath);
}
```

`ReadManifest` 会检查：路径非空 → 文件存在 → 扩展名为 `.fep` → 包内有 `plugin.json` → 清单可解析且字段合法 → 包内存在 `assembly` 指定的 DLL。

### 11.3 打包脚本

```powershell
# 用法
scripts\pack_fep.ps1 -ProjectDir <项目目录> [-Configuration Release] [-OutputDir <目录>] [-NoBuild]
```

脚本流程：

1. 定位项目内 `*.csproj`，除非指定 `-NoBuild` 否则先 `dotnet build -c <Configuration>`
2. 从 `bin\<Configuration>\net8.0\` 读取 `plugin.json` 与清单中的 `assembly` DLL
3. 复制到临时目录，ZIP 打包为 `<插件id>.fep`
4. 输出到 `-OutputDir`（若指定）或项目目录

### 11.4 构建时自动打包

`examples\Directory.Build.targets` 为示例项目提供 `AfterTargets="Build"` 的自动打包：只要项目目录存在 `plugin.json`，构建后即自动调用 `pack_fep.ps1 -NoBuild`，产物落在 `bin\<Configuration>\net8.0\`。

---

## 12. 程序集隔离与依赖

外部插件由 `PluginAssemblyLoadContext`（可卸载）加载：

- **每个插件独立上下文**，与主程序及其他插件隔离，避免程序集冲突。
- 通过 `AssemblyDependencyResolver` 解析插件自带的依赖。
- **特例**：若插件引用了 `FuckETS.PluginSdk`，加载时会**映射到主程序已加载的同一 SDK 程序集**，保证类型标识一致。

> **因此：不要把你引用到的 `FuckETS.PluginSdk.dll` 打进 `.fep` 包。** 只需打入口程序集（以及你自己额外依赖的、不在 SDK/主程序中的库）。

移除插件时，加载上下文会被 `Unload()`，使其可被 GC 回收。

---

## 13. 启用状态与持久化

插件配置存放在：

```
%APPDATA%\FuckETS\plugins.json
```

结构：

```json
{
  "Enabled": {
    "com.example.myplugin": true
  },
  "SelectedParserId": "fuckets.builtin.guangdong-highschool"
}
```

- `Enabled`：插件的**禁用表**（内部反向存储，`true` 表示禁用）。
- `SelectedParserId`：当前选中的解析插件 id。

行为规则：

- 内置广东高中解析插件（`fuckets.builtin.guangdong-highschool`）**始终存在且不可移除**，但可禁用。
- 当前选中的解析插件被禁用/移除时，自动回退到内置插件，界面会给出提示。
- 所有**启用的**解析插件都会出现在主界面下拉框；若无任何启用解析插件，则「解析」按钮禁用。
- 插件启用/禁用的变更会触发 `PluginsChanged`，主界面据此刷新下拉框并重建工具栏。

---

## 14. 调试与排错

**日志位置**：`%APPDATA%\FuckETS\logs\app_yyyyMMdd.log`（按日分文件）

记录内容包括：应用启动/退出、插件加载/移除、钩子异常（含堆栈）、解析流程等。

**常见加载失败原因**：

| 现象 | 原因 |
| --- | --- |
| 插件未出现在列表 | 未放入 `plugins\`，或扩展名不是 `.fep` |
| 「清单内容为空 / 解析失败」 | `plugin.json` 缺失或 JSON 非法 |
| 「插件类型 type 缺失或非法」 | `type` 字段缺失或不是 `behavior`/`parser` |
| 「API 版本不兼容」 | `apiVersion` 主版本与 SDK 不一致 |
| 「包内缺少程序集」 | `.fep` 内的 DLL 名与 `assembly` 字段不匹配 |
| 「入口类型不存在」 | `entryType` 全限定名写错 |
| 「未实现 IPlugin」 | 入口类型没有实现 `IPlugin`（或 `IBehaviorPlugin`/`IParserPlugin`） |
| 插件已存在，跳过 | 同一 `id` 已被加载 |

**行为插件不生效？**

- 确认已启用（「插件管理」中状态为「已启用」）
- 确认订阅发生在 `RegisterHooks` 中，而不是 `OnLoad`
- 确认钩子名与上下文类型匹配（`e is XxxContext ctx` 判断）
- 查看日志中的「插件钩子调用异常」

**开发期快速验证**：

- 行为插件可用 `System.Diagnostics.Debug.WriteLine(...)` 输出到调试器（见 `HelloBehaviorPlugin`）。
- 解析插件可先参考 `DemoParserPlugin` 输出占位内容，确认调度与下拉框切换正常，再接入真实格式。

---

## 15. 线程模型与安全须知

- **解析流程钩子可能在后台线程触发**（扫描、解析均在 `Task.Run` 中）；处理器须自行保证线程安全，避免直接操作 UI。
- **界面钩子**（`ui.*`）在 UI 线程触发。
- 钩子异常、插件加载异常均被**隔离并记录日志**，不会导致主程序崩溃或中断解析。
- 插件**不得修改** `%APPDATA%\ETS\` 下的原始数据；只应通过上下文对象影响输出。
- 移除插件会删除其 `.fep` 文件并注销其钩子；临时解包目录位于 `%TEMP%`。
- 若非必要，避免在插件中启动后台线程或长时间阻塞（会拖慢解析/界面响应）。

---

## 16. 完整示例骨架

### 16.1 解析插件骨架

```csharp
using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Models;

namespace MyParser;

public sealed class MyParser : IParserPlugin
{
    private const string PartA = "PartA";
    private const string PartB = "PartB";

    public string RegionId => "My-Region";

    public void OnLoad() { }

    public PluginManifest GetManifest() => new()
    {
        Id = "com.example.my-parser",
        Name = "我的地区解析",
        Version = "1.0.0",
        Author = "你",
        Type = PluginType.Parser,
        Region = RegionId,
        Assembly = "MyParser.dll",
        EntryType = typeof(MyParser).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "适配 My-Region 题型。",
    };

    public SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context)
    {
        var result = new SortedDictionary<string, List<SubItem>>(StringComparer.Ordinal)
        {
            [PartA] = new(),
            [PartB] = new(),
        };

        foreach (var item in context.ContentItems)
        {
            var part = Detect(item.Path);   // 你的判定逻辑
            if (part is not null && result.ContainsKey(part))
                result[part].Add(item);
        }
        return result;
    }

    public string GetPartLabel(string part) => part switch
    {
        PartA => "朗读",
        PartB => "问答",
        _ => part,
    };

    public IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath)
    {
        var lines = new List<string>();
        // 读取 contentJsonPath，按 part 解析，追加符合约定的正文行
        // ...
        return lines;
    }

    private static string? Detect(string dir) => null; // TODO
}
```

### 16.2 行为插件骨架

```csharp
using FuckETS.PluginSdk.Abstractions;
using FuckETS.PluginSdk.Context;
using FuckETS.PluginSdk.Context.Hooks;
using FuckETS.PluginSdk.Models;

namespace MyBehavior;

public sealed class MyBehavior : IBehaviorPlugin
{
    public PluginManifest GetManifest() => new()
    {
        Id = "com.example.my-behavior",
        Name = "我的行为插件",
        Version = "1.0.0",
        Type = PluginType.Behavior,
        Assembly = "MyBehavior.dll",
        EntryType = typeof(MyBehavior).FullName ?? string.Empty,
        ApiVersion = PluginApiVersion.Current,
        Description = "改写解析结果。",
    };

    public void OnLoad() { }

    public void RegisterHooks(HookEventSink events)
    {
        // 结果加工
        events.Subscribe(HookNames.ParseCompleted, 100, (_, e) =>
        {
            if (e is ParseCompletedContext ctx)
                ctx.OutputLines.Insert(0, "[由 MyBehavior 处理]");
        });

        // 过滤扫描结果
        events.Subscribe(HookNames.ScanCompleted, priority: 0, (_, e) =>
        {
            if (e is ScanCompletedContext ctx)
                ctx.Folders.RemoveAll(f => f.Name.Length < 6);
        });
    }
}
```

---

## 17. 最佳实践与 FAQ

**最佳实践**

1. 插件清单的关键字段（id/type/assembly/entryType/apiVersion）在 `plugin.json` 与 `GetManifest()` 中保持一致。
2. 行为插件只在 `RegisterHooks` 中订阅钩子；解析插件不要注册钩子。
3. 输出行严格遵循[第 10 节](#10-输出行约定pdf--高亮依赖)的格式，保证高亮与 PDF 排版正确。
4. 解析插件为所有支持的 Part 返回条目（即使为空），以保留「未找到」提示。
5. 钩子处理器要健壮：**抛出异常虽然被隔离，但会导致该处理器逻辑失效**，建议自行捕获并记录。
6. 需要跨钩子传数据时使用 `ParseContext.Shared`，不要用静态字段。
7. 不要打包 `FuckETS.PluginSdk.dll`。

**FAQ**

- **Q：一个插件能同时是解析插件和行为插件吗？**
  A：不能。`type` 只能二选一。
- **Q：内置插件可以替换吗？**
  A：内置插件不可移除，但可禁用；禁用后若没有其他解析插件，则无法解析。你可以在下拉框中切换到自己的解析插件。
- **Q：能否修改解析用的 JSON 路径？**
  A：当前 `part.parse.before` 只支持 `Cancel`；`JsonPath` 是只读的。请在 `parse.completed` 或 `part.parse.after` 做后处理。
- **Q：插件异常会不会导致 FuckETS 崩溃？**
  A：不会。加载异常与钩子异常都会被隔离并写日志。
- **Q：解析流程钩子所在线程？**
  A：可能在后台线程。不要直接操作 UI，改用 `ui.*` 钩子或在处理器内自行调度。

---

## 18. 附录：SDK 类型速查

**命名空间**

| 命名空间 | 主要类型 |
| --- | --- |
| `FuckETS.PluginSdk.Abstractions` | `IPlugin`、`IBehaviorPlugin`、`IParserPlugin`、`PluginType`、`PluginApiVersion`、`HookEventSink`、`HookHandler` |
| `FuckETS.PluginSdk.Context` | `HookNames`、`ParseContext`、`SubItem` |
| `FuckETS.PluginSdk.Context.Hooks` | `HookContext` 及各 `XxxContext`、`FolderItemInfo`、`PdfExportOption`、`ToolbarButtonInfo` |
| `FuckETS.PluginSdk.Models` | `PluginManifest`、`PluginInfo`、`ManifestValidator`、`ManifestValidation` |
| `FuckETS.PluginSdk.Packaging` | `FepPackager`、`FepReadResult` |

**关键常量**

- `PluginApiVersion.Current` = `"1.0"`
- `FepPackager.ManifestFileName` = `"plugin.json"`
- `FepPackager.Extension` = `".fep"`
- 内置插件 id = `fuckets.builtin.guangdong-highschool`

**外部资源**

- 示例插件：`examples/HelloBehaviorPlugin`、`examples/ToolbarDemoPlugin`、`examples/DemoParserPlugin`
- 内置解析插件实现参考：`Plugins/BuiltInGuangdongPlugin.cs`
- 打包脚本：`scripts/pack_fep.ps1`
- 插件宿主实现：`Plugins/PluginHost.cs`
