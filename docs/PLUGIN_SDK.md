# FuckETS 插件开发指南（Plugin SDK 1.0）

FuckETS 通过插件体系支持多地区/多实体的试题格式与功能扩展。插件分两类：

| 类型 | 接口 | 作用 |
| --- | --- | --- |
| **行为插件**（behavior） | `IBehaviorPlugin` | 订阅钩子（Hook），干预扫描、解析、展示、PDF 导出等流程 |
| **解析插件**（parser） | `IParserPlugin` | 实现特定地区/实体的 Part 识别与答案解析，替换默认解析器 |

插件契约全部位于 **`FuckETS.PluginSdk`** 类库。插件**只依赖 SDK，不引用主程序内部类型**。

## 快速开始

1. 新建类库项目（`net8.0`），引用仓库内 `FuckETS.PluginSdk`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\FuckETS.PluginSdk\FuckETS.PluginSdk.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="plugin.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

2. 在项目根放 `plugin.json` 清单，实现接口（见下文）。
3. 打包为 `.fep`：

```powershell
scripts\pack_fep.ps1 -ProjectDir path\to\YourPlugin
```

4. 主界面「插件管理…」→「加载 .fep 插件…」安装；或把 `.fep` 放入程序目录 `plugins\` 后重启。
5. 解析插件还会出现在主界面「解析插件」下拉框中供切换。

可参考完整示例：`examples/HelloBehaviorPlugin` 与 `examples/ToolbarDemoPlugin`（行为）以及 `examples/DemoParserPlugin`（解析）。

## plugin.json 清单

```json
{
  "id": "example.hello-behavior",
  "name": "示例·行为插件",
  "version": "1.0.0",
  "author": "作者",
  "type": "behavior",
  "region": "Demo-Region",
  "assembly": "HelloBehaviorPlugin.dll",
  "entryType": "HelloBehaviorPlugin.HelloBehavior",
  "apiVersion": "1.0",
  "description": "一句话说明"
}
```

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `id` | ✔ | 全局唯一（建议反向域名）；重复 id 会被拒绝加载 |
| `name` / `version` / `author` / `description` | name ✔ | 展示元数据 |
| `type` | ✔ | `behavior` 或 `parser`（缺失或非法即拒绝） |
| `assembly` | ✔ | 包内入口程序集文件名（如 `MyPlugin.dll`） |
| `entryType` | ✔ | 入口类型完全限定名（须实现 `IPlugin`） |
| `apiVersion` | ✔ | 主版本须与 SDK 当前版本（`PluginApiVersion.Current` = `1.0`）一致 |
| `region` | 解析插件建议 | 地区/实体标识，如 `Guangdong-HighSchool` |

校验不通过时插件被拒绝加载，原因写入日志（`%APPDATA%\FuckETS\logs\`）。

## 接口契约

### IPlugin（基础）

```csharp
public interface IPlugin
{
    PluginManifest GetManifest();   // 返回与 plugin.json 一致的清单
    void OnLoad();                  // 加载时调用一次（勿在此注册钩子，见 IBehaviorPlugin）
}
```

### IBehaviorPlugin（行为插件）

```csharp
public interface IBehaviorPlugin : IPlugin
{
    void RegisterHooks(HookEventSink events);
}
```

主程序在插件**启用时**调用 `RegisterHooks`，禁用/移除时自动注销该插件程序集注册的全部订阅。请只在 `RegisterHooks` 内订阅钩子。

```csharp
events.Subscribe(HookNames.ParseCompleted, priority: 100, (_, e) =>
{
    if (e is ParseCompletedContext ctx)
        ctx.OutputLines.Add("追加一行");
});
```

- `priority` 越小越先执行；同一钩子内按优先级链依次调用。
- **单个处理器抛异常会被隔离并记录日志，不会中断主流程。**

### IParserPlugin（解析插件）

```csharp
public interface IParserPlugin : IPlugin
{
    string RegionId { get; }
    SortedDictionary<string, List<SubItem>> DetectParts(ParseContext context);
    string GetPartLabel(string part);
    IReadOnlyList<string> ParsePart(ParseContext context, string part, string contentJsonPath);
}
```

宿主调度流程（`PluginHost.RunParse`）：

1. 枚举作业目录下 `content_<数字>` 子目录 → `context.ContentItems`（`SubItem(Id, Path)`）
2. 投递 `part.detect.before`（逐目录）→ 调用 `DetectParts`
3. 投递 `part.detect.after`（逐目录，可改写 `Part` 归属）→ 每个 Part 取 Id 最大者
4. 对每个 Part：投递 `part.parse.before`（`Cancel=true` 可跳过整个 Part）→ 输出标题行 `\n【PartX】 标签` → 调用 `ParsePart` → 投递 `part.parse.after`
5. 无命中目录的 Part 输出 `\n【PartX】 未找到对应的子文件夹`
6. 投递 `parse.completed`（可改写全部输出行）

因此 **`ParsePart` 只返回正文行，不含【Part】标题**；`DetectParts` 应为所有已知 Part 键返回条目（哪怕列表为空），以保证「未找到」提示按预期输出。

#### 输出行约定（PDF / 高亮依赖）

正文行须遵循以下格式，`LineClassifier` 与 PDF 样式据此着色：

- 问题行：`【问题 1】 问题文本`
- 候选答案引导行：`  候选答案：`
- 答案行：`    1. 答案文本`（续行缩进 7 空格）
- 错误行以 `错误：` / `[PartX 解析错误]` 开头
- 空白行用于段落分隔；**不要输出文件夹名、检测信号、完成时间等无关元数据**

## 钩子目录

| 钩子名 | 上下文 | 时机 / 可修改内容 |
| --- | --- | --- |
| `scan.completed` | `ScanCompletedContext` | 扫描+标题加载后；`Folders` 可增删改（路径无效的条目会被丢弃） |
| `ui.folderList.loaded` | `FolderListLoadedContext` | 列表绑定到界面后（通知型） |
| `part.detect.before` | `PartDetectBeforeContext` | 单个 content 目录识别前（只读） |
| `part.detect.after` | `PartDetectAfterContext` | 识别后，可改 `Part` 归属 |
| `part.parse.before` | `PartParseBeforeContext` | 解析前，可改 `JsonPath`；`Cancel=true` 跳过该 Part |
| `part.parse.after` | `PartParseAfterContext` | 解析后，可读 `ProducedLines` |
| `parse.completed` | `ParseCompletedContext` | 全部解析完成后，可改写 `OutputLines` |
| `ui.result.displaying` | `ParseResultDisplayingContext` | 结果展示前，可改写展示行 |
| `ui.pdf.exporting` | `PdfExportingContext` | PDF 导出前，可改 `OutputText` / `FileName` / `Options`（键：`fontSize`、`titleFontSize`、`partHeadingFontSize`、`marginMm`、`lineHeight`、`boldHeadings`） |
| `ui.mainWindow.toolbar` | `MainWindowToolbarContext` | 主界面构建时；`AddButton` 追加工具栏按钮，`ShowMessage` 弹出信息框；插件启停时会重新投递以重建按钮 |

注意：解析流程钩子可能在**后台线程**触发，处理器须自行保证线程安全；界面钩子在 UI 线程。

## .fep 包格式

`.fep` = zip 压缩包，根目录包含：

```
your.plugin.id.fep
├── plugin.json          # 清单
└── YourPlugin.dll       # 入口程序集（由 assembly 字段指定）
```

- 依赖的 `FuckETS.PluginSdk.dll` **不要打进包内**——加载时会映射到主程序已加载的 SDK。
- 程序使用 `AssemblyLoadContext`（可卸载）加载，插件间与主程序程序集隔离。
- API 兼容规则：`apiVersion` 主版本须等于当前 SDK 主版本，否则拒绝加载。

## 启用状态与持久化

- 启用/禁用、当前选中的解析插件保存在 `%APPDATA%\FuckETS\plugins.json`。
- 内置广东高中解析插件（`fuckets.builtin.guangdong-highschool`）**始终存在且不可移除**，但可禁用。
- 当前选中的解析插件被禁用/移除时，自动回退到内置插件（界面会提示）。
- 所有启用的解析插件都会出现在主界面下拉框；无任何启用解析插件时禁用「解析」按钮。

## 示例插件

| 示例 | 类型 | 演示内容 |
| --- | --- | --- |
| `examples/HelloBehaviorPlugin` | 行为 | 启用后每次解析在结果末尾追加 `[示例插件] …` 标记行；禁用即消失——用于验证钩子生效 |
| `examples/ToolbarDemoPlugin` | 行为 | 主界面工具栏显示「示例插件」按钮，点击弹出信息框（标题「示例插件」、内容「FuckETS 示例行为插件」）；用于验证 UI 扩展钩子与启停时按钮重建 |
| `examples/DemoParserPlugin` | 解析 | 全部 content 目录归入 PartA 并输出占位内容；用于验证解析插件切换与调度 |

打包并加载后可在「插件管理」中切换启用状态直观验证。

## 线程与安全须知

- 钩子异常、插件加载异常均被隔离并写日志，不会导致主程序崩溃或中断解析。
- 插件不得修改 `%APPDATA%\ETS\` 下的原始数据；只通过上下文对象影响输出。
- 移除插件会删除其 `.fep` 文件并注销其钩子；内置插件不可移除。
