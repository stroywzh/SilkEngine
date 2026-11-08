# AGENTS.md

本文件给在本仓库工作的智能体提供事实性上下文。所有结论以仓库现有文件为准：`SilkEngine.slnx`、各 `*.csproj`、`Directory.Build.props`、`src/SandBox/README.md`、`RULE.md`。**如与代码冲突，以代码为准。**

> `AGENTS.md` 与 [`RULE.md`](RULE.md) 均为受保护文件，修改规则见下文「项目约束」。

---

## 1. 项目技术栈

| 维度 | 内容 | 依据 |
| --- | --- | --- |
| 语言 / 运行时 | C# / .NET 10，目标框架 `net10.0`；源生成器项目为 `netstandard2.0` | 各 `.csproj` 的 `<TargetFramework>` |
| 项目 SDK | `Microsoft.NET.Sdk`（全部项目） | `.csproj` 的 `<Project Sdk="...">` |
| 解决方案 | `SilkEngine.slnx`（**.slnx 格式，仓库内没有 `.sln`**） | 仓库根目录 |
| 编译开关 | `Nullable=enable`、`ImplicitUsings=enable`、`AllowUnsafeBlocks=true` | `Directory.Build.props` |
| 图形栈 | Silk.NET 2.23.0（`Silk.NET`、`Silk.NET.Core`、`Silk.NET.OpenGL`）→ OpenGL 后端 | `src/SilkEngine.Rendering/SilkEngine.Rendering.csproj` |
| 源生成 | Roslyn `Microsoft.CodeAnalysis.CSharp` 4.14.0、`Microsoft.CodeAnalysis.Analyzers` 3.11.0、`Microsoft.Bcl.HashCode` 1.1.1；`IIncrementalGenerator` | `src/SilkEngine.SourceGen/SilkEngine.SourceGen.csproj` |
| 测试 | xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.14.1 + `xunit.runner.visualstudio` 2.8.2 | `tests/SilkEngine.Asset.Tests/SilkEngine.Asset.Tests.csproj` |
| 代码风格 | 根 `.editorconfig`：4 空格缩进、CRLF、UTF-8、不修剪行尾空格 | 仓库根 `.editorconfig` |
| 本机 SDK | `dotnet --version` = 10.0.300（8.0.416 / 9.0.309 / 10.0.300 已安装） | 生成本文件时实测 |

配置约定：
- 无 `global.json`、无 `.config/dotnet-tools.json`，即未锁定 SDK 小版本、未声明本地工具。
- `src/SilkEngine` 与 `src/SilkEngine.Rendering` 打开 `EmitCompilerGeneratedFiles`，生成代码输出到各自 `obj/generated`；`SilkEngine.csproj` 还会在 `Build` 后把生成代码镜像到仓库根 `Generated/`（见其 `MirrorGeneratedSourcesForReview` Target）。`Generated/` 是构建产物，不要手改，也不要拷回 `src/`（csproj 注释说明重复包含会导致 `CS0121`）。
- `src/SandBox` 使用自定义输出目录 `src/SandBox/Build/<Configuration>/net10.0/`（`OutputPath` 在 `SandBox.csproj` 中重定义），不是默认 `bin/`。

---

## 2. 项目依赖关系

### 2.1 项目与依赖表（以 `ProjectReference` / `PackageReference` 为准）

| 项目 | 路径 | 类型 / 目标框架 | ProjectReference | PackageReference |
| --- | --- | --- | --- | --- |
| `SilkEngine.SourceGen` | `src/SilkEngine.SourceGen` | 类库 · `netstandard2.0` · Roslyn 源生成器 | 无 | `Microsoft.CodeAnalysis.CSharp` 4.14.0；`Microsoft.CodeAnalysis.Analyzers` 3.11.0（`PrivateAssets=all`）；`Microsoft.Bcl.HashCode` 1.1.1（`PrivateAssets=all`） |
| `SilkEngine` | `src/SilkEngine` | 类库 · `net10.0` · **框架层** | `SilkEngine.SourceGen`（`OutputItemType=Analyzer`，`ReferenceOutputAssembly=false`） | 无 |
| `SilkEngine.Asset` | `src/SilkEngine.Asset` | 类库 · `net10.0` · **内容管线层** | `SilkEngine` | 无 |
| `SilkEngine.Rendering` | `src/SilkEngine.Rendering` | 类库 · `net10.0` · **OpenGL 渲染后端** | `SilkEngine`；`SilkEngine.SourceGen`（Analyzer） | `Silk.NET` 2.23.0、`Silk.NET.Core` 2.23.0、`Silk.NET.OpenGL` 2.23.0 |
| `SandBox` | `src/SandBox` | 可执行（`Exe`）· `net10.0` · **业务黑盒示例** | `SilkEngine.Rendering`、`SilkEngine`、`SilkEngine.Asset` | 无 |
| `SilkEngine.Asset.Tests` | `tests/SilkEngine.Asset.Tests` | 测试 · `net10.0` · xUnit | `SilkEngine`、`SilkEngine.Asset`、`SilkEngine.Rendering` | `Microsoft.NET.Test.Sdk` 17.14.1、`xunit` 2.9.3、`xunit.runner.visualstudio` 2.8.2 |

### 2.2 依赖图

```text
SilkEngine.SourceGen  (netstandard2.0, Roslyn 分析器；无项目依赖)
        ^                         ^
        | Analyzer 引用            | Analyzer 引用
        | (ReferenceOutputAssembly=false)
        |                         |
SilkEngine (net10.0, 框架层) <---- SilkEngine.Rendering (net10.0, OpenGL 后端)
        ^
        | ProjectReference
SilkEngine.Asset (net10.0, 内容管线)

SandBox (Exe) ──► SilkEngine.Rendering
              ──► SilkEngine
              ──► SilkEngine.Asset

SilkEngine.Asset.Tests ──► SilkEngine
                       ──► SilkEngine.Asset
                       ──► SilkEngine.Rendering
```

要点：
- **唯一反向依赖方向**：`SilkEngine`（框架）不引用 `SilkEngine.Asset` / `SilkEngine.Rendering`；后两者单向依赖框架层。`SandBox` 是唯一同时依赖三层的可执行项目（组合根）。
- `SilkEngine.SourceGen` 只以 **Analyzer** 方式被引用（`ReferenceOutputAssembly=false`），不是运行时引用；它同时挂在 `SilkEngine` 和 `SilkEngine.Rendering` 两个运行时项目上。
- 命名空间注意：`SilkEngine.Rendering.*` 与 `SilkEngine.Asset.*` 这两个命名空间在**框架程序集 `SilkEngine`** 中提供抽象/命令/模型，具体实现位于 `SilkEngine.Rendering` 与 `SilkEngine.Asset` 项目。
- `InternalsVisibleTo`：`SilkEngine` → `SilkEngine.Asset`、`SilkEngine.Asset.Tests`；`SilkEngine.Asset` → `SilkEngine.Asset.Tests`（内部实现只对这两个程序集开放，业务程序集不开放）。
- `SilkEngine.slnx` 收录 5 个项目：`SandBox`、`SilkEngine.SourceGen`、`SilkEngine.Asset`、`SilkEngine.Rendering`、`SilkEngine`。**`tests/SilkEngine.Asset.Tests` 不在解决方案中**，且 `.gitignore` 忽略了 `tests/`、`docs/`、`*.g.cs`、`[Bb]uild/`。

---

## 3. `src/` 目录指引

| 路径 | 职责 | 关键入口 |
| --- | --- | --- |
| `src/SandBox/` | 业务黑盒示例程序（Exe）：组合根，只负责装配后端/管线/内容引导并加载纹理；使用方只需了解业务 API | `Program.cs`（顶层语句入口）、`ConsoleSetup.cs`、`Assets/` |
| `src/SilkEngine/` | 框架层程序集：宿主启动、应用注册、资产门面、数学/时间/日志等基础设施 | `EngineHost.Start()` / `EngineApp.Run()`、`Application`、`Assets` |
| `src/SilkEngine/Asset/` | 资产模型与契约：资产 ID/GUID、目录项、加载器接口、负载类型、启动模式 | `AssetId`、`IAsset`、`IAssetLoader`、`TextureAssetLoader` |
| `src/SilkEngine/Core/AssetSystem/` | 资产运行时：命令调度、资产存储、路径解析、启动引导、渲染侧桥接 | `AssetRuntime`、`AssetBootstrap`、`AssetDispatcher` |
| `src/SilkEngine/Core/IO/` | IO 基础设施：原子文件写、内容哈希、路径规范化 | `AtomicFile`、`ContentHash`、`PathUtil` |
| `src/SilkEngine/Core/LogSystem/` | 日志下沉实现：控制台/文件 sink、异步 IO 队列、日志路径与格式 | `FileLogSink`、`LogIoQueue`、`LogPaths` |
| `src/SilkEngine/Core/ObjectSystem/` | 对象系统：对象生命周期、容器、创建令牌、作用域 | `ObjectManager`、`ObjectStore`、`ObjectLifecycle` |
| `src/SilkEngine/Rendering/` | 渲染抽象与基础实现：命令缓冲/队列、管线与后端基类、帧上下文、渲染项、渲染队列分组 | `ICommandBuffer`、`CommandBuffer`、`RenderPipelineBase`、`IRenderBackend` |
| `src/SilkEngine/Rendering/RenderCommands/` | 内置渲染命令声明（配合源生成器生成命令表与录制扩展） | `DrawCommand`、`SetViewportCommand`、`BindTextureCommand` |
| `src/SilkEngine/Rendering/Assets/` | 资产到渲染资源的绑定与上传 | `TextureUploader`、`TextureBinding`、`AssetRuntimeCache` |
| `src/SilkEngine/Rendering/Internal/` | 渲染排序键打包等内部实现（非公共契约） | `RenderItemSortKeyBuilder`、`SortKeyPacker` |
| `src/SilkEngine/Runtime/` | 引擎线程模型与更新调度 | `EngineThread`、`ThreadFactory`、`ThreadRegistry`、`UpdateScheduler` |
| `src/SilkEngine/Runtime/Host/` | 主循环宿主：主循环、渲染线程、资产泵 | `MainLoop`、`RenderThread`、`AssetPump` |
| `src/SilkEngine/Scripting/` | 场景对象 API | `GameObject`、`Component`、`Transform`、`ObjectId` |
| `src/SilkEngine/Serialization/` | JSON 编解码与转换器注册 | `EngineJson`、`JsonCodec`、`JsonConverterRegistry` |
| `src/SilkEngine/Mathematics/` | 数学值类型与标记属性（源生成器输入之一） | `Vector2Int`、`Vector4`、`MathValueAttribute` |
| `src/SilkEngine/Internal/` | 框架内部工厂 | `ComponentFactory` |
| `src/SilkEngine.Asset/` | 内容管线程序集：扫描 `Assets/`、导入、目录与制品管理、边车（sidecar）、启动引导 | `ContentAssets.CreateBootstrap()`、`AssetScanner`、`AssetCatalog`、`ArtifactStore` |
| `src/SilkEngine.Asset/Abstractions/` | 内容管线扩展点接口 | `IAssetImporter`、`IAssetCatalogAdapter`、`ISidecarCodec` |
| `src/SilkEngine.Asset/Decoder/Png/` | PNG 解码器（框架内部实现） | `PngDecoder`、`PngHeader`、`PngChunk` |
| `src/SilkEngine.Asset/Transfer/` | 制品引用 | `ArtifactRef` |
| `src/SilkEngine.Rendering/` | Silk.NET/OpenGL 渲染后端程序集：实现框架层渲染抽象 | `SilkOpenGLBackend`、`BuiltInPipeline`、`OpenGLDevice` |
| `src/SilkEngine.Rendering/BuiltIn/` | 内置渲染管线与演示几何、GL 设备实现 | `BuiltInPipeline`、`OpenGLDevice`、`DebugTriangle`、`TexturedQuad` |
| `src/SilkEngine.Rendering/Constants/` | 常量缓冲绑定与 2D 绘制常量 | `ConstantArena`、`ShaderConstantBindings`、`DrawConstants2D` |
| `src/SilkEngine.SourceGen/` | Roslyn 增量源生成器：渲染命令表/录制扩展、数学值类型代码生成 | `RenderCommandSourceGen`、`MathValueSourceGen` |
| `src/SilkEngine.SourceGen/Diagnostics/` | 生成器诊断 ID 分区定义 | `DiagnosticBands` |

（仅列 `src/` 下源码目录；`bin/`、`obj/`、`Build/` 等生成物不在此表。）

---

## 4. 项目约束（[`RULE.md`](RULE.md) 摘要）

> 原文路径：[`RULE.md`](RULE.md)（仓库根目录，受保护文件）。以下为摘要，冲突时以原文为准。

- **授权时效**：所有授权**仅对当次操作有效**，不延续到后续变更。
- **受保护文件**：`RULE.md` 与 `AGENTS.md`（即使尚不存在也算）。修改须获得用户**单次明确授权**，且仅允许**追加新内容**或**修改本次新增部分**，严禁改已有内容。
- **批量更改审批**：涉及 **3 个及以上**代码文件，或与代码紧密相关的文件（配置、资源等）的修改，须事先获得用户明确授权。
- **设计文档**：未经用户要求，`spec` / `design` / `plan` 类文档不得提交到 Git。
- **调试文件**：可参考 `debug`/`Debug` 等目录，但不得把其代码直接复制进正式代码；复用需重写（通用工具方法除外）。
- **架构原则**：组合优于继承，优先接口；优先扩展性与长期维护，提供接口抽象、解耦、配置分离；框架重构应减少调用方负担（统一命名、简化参数、提供默认实现）。
- **反射/`dynamic`**：允许有限使用，仅限初始化或少量低频调用；运行时逻辑禁止高频调用。
- **TDD 边界**：测试代码不得侵入业务/框架层——**不允许**为通过测试给原有代码新增公开 `API`、属性、方法或字段。
- **工作区边界**：未经允许不得查看/更改工作目录外内容；`git` 相关命令须先询问用户。
- **注释规范（D12，随 RULE.md 同文）**：注释越短越好；public 成员写调用方契约（作用、必要的返回值/自定义异常），不写实现机制；`protected override` 用 `/// <inheritdoc/>`，行为不同时补差异；`internal`/`private` 不强制文档注释。禁止名称复述、禁止写任务/提案编号（如 C03、REN-N-08）、禁止在 public 文档里描述 GL/D3D 等后端实现细节。只改注释时不得改签名与行为。
- **文档/大范围检索**：进行文档编写或代码库大范围检索时，应使用子代理。

---

## 5. 常用构建、运行、测试命令

以下命令可从仓库文件确认（`SilkEngine.slnx`、各 `.csproj`、`src/SandBox/README.md`）。在仓库根目录 `E:\CSharp\SilkEngine` 执行：

| 用途 | 命令 | 说明 |
| --- | --- | --- |
| 构建解决方案 | `dotnet build SilkEngine.slnx` | 构建 slnx 收录的 5 个项目（**不含** `tests/`，测试项目不在 slnx 中） |
| 构建单项目 | `dotnet build src/SilkEngine/SilkEngine.csproj` | 其他项目同理替换路径 |
| 运行示例 | `dotnet run --project src/SandBox/SandBox.csproj -- --frames 180` | `--frames N` 跑满 N 帧后退出；`Program.cs` 的 `DefaultFrames` 为 50000 |
| Release 运行 | `dotnet run --project src/SandBox/SandBox.csproj -c Release -- --frames 180` | 输出到 `src/SandBox/Build/Release/net10.0/` |
| 运行测试 | `dotnet test tests/SilkEngine.Asset.Tests/SilkEngine.Asset.Tests.csproj` | xUnit；因不在 slnx 中，需显式指定项目路径 |
| 查看 SDK | `dotnet --version` | 本机为 10.0.300 |

补充事实（来自 `src/SandBox/README.md` 与代码）：
- 运行时会先 `ConsoleSetup` 准备控制台（设置标题、UTF-8，Windows 下关闭 QuickEdit），再启动引擎；`EngineHost.Start(...)` 依次解析后端/管线、执行内容引导、启动主循环。
- 日志始终写入输出目录：`src/SandBox/Build/<Configuration>/net10.0/Log/Log<yyyy-MM-dd>.log`；引擎日志只写文件，控制台仅保留横幅与里程碑行。
- `SandBox` 的 `Assets/**` 通过 csproj 的 `None Update ... CopyToOutputDirectory="PreserveNewest"` 复制到输出目录。

---

## 6. 协作提示

- 修改前先读相关源码与 csproj，不要凭猜测改依赖关系；依赖关系变更须与 `SilkEngine.slnx`、`ProjectReference` 保持一致。
- 新增带 `[RenderCommand]` 的命令结构体、或在渲染/框架项目里使用 `MathValue` 标记时，生成代码由 `SilkEngine.SourceGen` 产出，不要手写或复制生成结果。
- `tests/` 与 `docs/` 被 `.gitignore` 忽略，新增测试不会被版本库跟踪（如需提交须先与用户确认）。
- 涉及 3 个及以上文件的改动、或触及受保护文件前，先取得用户单次明确授权。
