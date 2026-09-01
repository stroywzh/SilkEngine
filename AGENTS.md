# SilkEngine

基于 Silk.NET 的通用游戏引擎原型，C# / .NET 10 / OpenGL 4.6。当前处于框架原型阶段。

## 代码风格

- 所有公共 API 使用 C# 现代语法（init-only 属性、主构造函数、集合表达式）
- 命名空间：根命名空间不放置类型，全部归于子命名空间——`SilkEngine.Core`（含 `.Core.Assets`）/ `.Scene` / `.Render` / `.Assets`（含 Binding/Importer/Serialization/VirtualFileSystem）/ `.Rendering`（含 Abstraction/Backend/OpenGL/Pipeline）/ `.Threading` / `.InputSystem` / `.Math` / `.Host`；命名空间与物理目录/程序集解耦（如 `SilkEngine.Render` 类型分布于 Assets 与 Rendering.OpenGL 项目）
- 静态门面与实例模式：`Time` / `Input` / `Log` 为全局门面；`SceneManager` / `AssetManager` 为实例类（EngineHost 组合根创建并注册进 Services，业务经 EngineHost 公开门面属性取用）
- 线程通过 `ThreadFactory.CreateThread` 统一创建（禁止直接 `new Thread()`）
- `allow(ArbirtaryCode)` requires safe code blocks explicitly: 仅 "unsafe" 标为 unsafe；所有其他代码逻辑应为 safe
- Priority: automated testing exists with full coverage (568 xUnit tests)

## 架构

### 线程模型

```
主线程 (Heartbeat, EngineLoop)
  ├─ Input.Update → FixedTick/Tick/LateTick
  ├─ drain PreRender → collect/freeze RenderPacket
  ├─ RenderThreadHost.Submit(阻塞等待 GPU 完成) → Scene.PostRender
  └─ CommitFrame(销毁/注册/快照 swap → drain FrameCommit → AssetManager 应用 Pipeline 结果)

ThreadRuntime（线程资源唯一属主）
  ├─ MainThreadDispatcher（PreRender / FrameCommit 批次）
  ├─ BackgroundScheduler（Worker Pool）
  ├─ RenderThreadHost（Rendering 专用线程、GPU 资源与 backend 退出释放）
  └─ ManagedLoopRegistry（internal；未来扫描/监听/批量循环）

AssetPipeline（Assets 域）
  ├─ VFS 索引 → AssetCatalog → BuildKey/依赖计划
  ├─ Worker Read/Decode/Import/Deserialize/Validate
  └─ Main FrameCommit → AssetManager 状态与 Payload 发布

Assets.AssetRenderBridge → Rendering.Abstraction 请求/Handle → Rendering.Backend → Rendering.OpenGL/Vulkan
```

### 核心子系统

- **程序集拆分**：按依赖方向拆为 8 个引擎程序集——`SilkEngine.Runtime`（Math/Core/Threading/Input）→ `SilkEngine.Rendering.Abstraction` + `SilkEngine.Rendering.Backend`（无资产语义契约，仅依赖 Runtime）→ `SilkEngine.Assets`（含 Render 域 Material*/MeshFactory）→ `SilkEngine.Scene` → `SilkEngine.Rendering`（RenderSystem/RenderThreadHost/Pipeline）→ `SilkEngine.Rendering.OpenGL` → `SilkEngine.Host`（组合根：EngineHost/EngineBuilder/EngineOptions + internal EngineLoop）。禁令：Rendering 域不得引用 Assets/Scene；Threading 不得引用 Rendering/Assets；Sandbox 仅直接引用 Host（`DependencyBoundaryTests` 断言）。跨程序集 friend access 保留给 `SilkEngine.Tests`
- **Services**: `public static class`（SilkEngine.Runtime）服务定位器：Register（重复注册抛错）/ Get（未注册 fail-fast）/ TryGet（初始化前静默回退）/ Unregister（测试夹具用）/ Shutdown（反序 Dispose 全部 IDisposable 服务并清空注册表，幂等）。EngineHost 组合根集中注册管理者实例，业务经 EngineHost 公开门面取用。`[Service(Priority, Name)]` 特性经 ServiceRegistrationGenerator 自动注册（ModuleInitializer，按 Priority 升序、类名次排序；仅 SilkEngine* 引擎程序集 SERV001/002 把关）
- **EngineLoop**: internal 心跳驱动器（位于 SilkEngine.Host，EngineHost.Loop 内部属性；业务用 SceneManager/AssetManager 门面），计算 dt（钳制 0.1s）→ 驱动 Input/Tick/渲染。内建 FixedStepAccumulator（LogicLoop 合并）；`Initialize` 执行 `SceneManager.Attach` 注入注册表与快照管理器并注册输入服务；依赖（RenderSystem/AssetManager/ThreadRuntime/ComponentRegistry/FrameSnapshotManager）全部由 EngineHost 经构造注入，资产管线组合收在 `AssetManager.CreateDiskBacked` 工厂；支持 Pause 和 Embedded 模式
- **FrameSnapshot/ComponentRegistry**: 帧原子性核心。ComponentRegistry 类型索引注册表（持久化 ComponentGroup + MonoBehaviour 基类索引 `_mbIndex` 按具体类型归类），FrameSnapshotManager 双缓冲快照，帧末 CommitPending 统一应用销毁/注册并 swap（零分配）。销毁幂等（`_destroyPending`/`_destroyed` 双标志），LoadScene 场景切换注销旧场景全部组件
- **Scene System**: Object → GameObject(内置Transform) → Component(活跃状态机: `RecomputeActiveState` 单一真理源, OnEnable/OnDisable/OnDestroy 下沉至 Component, Enabled/IsActive/SetParent 三路幂等重放) → MonoBehaviour(OnAwake/OnStart/OnUpdate/OnFixedUpdate/OnLateUpdate/OnPostRender)。工厂 `InitializeComponent`（挂载→OnAwake→RecomputeActiveState(Enable)→注册），GO 层级活跃门控 `IsActiveInHierarchy` 级联通知，`Started` 标志位 Start 补发，`AddObjectToScene` 运行时增删；SceneManager 为实例（ctor 订阅 Object.DestroyHandler，Dispose 解绑），`Attach(registry, snapshotManager)` 注入（替代 ActiveRegistry），Tick/FixedTick/LateTick/PostRender 经 `Registry.MonoBehaviourGroups` 基类索引直读派发（零 IsSubclassOf 扫描）
- **Rendering**: `Rendering` 负责 RenderSystem、RenderCollector、ForwardPipeline、RenderPacket/RenderFrame 和 RenderThreadHost；`Rendering.Abstraction` 定义无资产语义的数据/Handle，`Rendering.Backend` 定义后端能力契约，`Rendering.OpenGL`/`Rendering.Vulkan` 提供具体实现。整个 Rendering 域不引用或解析 AssetId、AssetHandle、AssetPipeline、AssetManager、AssetEntry 或 AssetPayload；Assets 侧通过 AssetRenderBridge 完成资产到渲染契约的转换
- **Asset System**: AssetPipeline 负责 VFS 索引后的 Identity/Plan、BuildKey 去重、依赖、Read/Decode/Import/Deserialize/Validate 与不可变 Payload 结果；AssetManager 是 Main 域运行时门面，负责 Payload 缓存、AssetOperation 发布、驻留和卸载。静态 `Asset.Load<T>(path)` 通过 Services 转发；未索引路径直接抛详细 `InvalidOperationException`，不自动补录
- **Serialization**: AssetSerializationRecord、Serializer Registry/Store 和 AssetSerializationService 属于 AssetPipeline 的 Worker/缓存阶段；Serializer 只处理 `IAssetPayload`，不创建 GPU 或 Scene 对象。当前 SilkEngine.SourceGen 仅含 [Service] 自动注册生成器（ServiceRegistrationGenerator）
- **Input**: Input门面 → KeyboardState/MouseState(双缓冲) → IInputProvider → SilkInputProvider
- **Threading**: ThreadRuntime 统一登记 Main/Worker/Render 域、拥有 BackgroundScheduler/MainThreadDispatcher/RenderThreadHost 生命周期并负责关闭。业务层只接触 `IBackgroundScheduler`、`IMainThreadDispatcher` 和 `IJobHandle`；AssetPipeline 第一阶段使用 Worker Pool，未来持续扫描/监听才使用 internal ManagedLoopRegistry。旧 ThreadManager/Request/Executor API 迁移后删除
- **Math**: 自研 Mathf/Vector2/Vector3/Quaternion/Matrix4x4 (左手系, 行主序约定; GL 上传 UniformMatrix4 transpose=true)
- **Log**: Log.Info/Warn/Error/Debug + StackTree + ILogWriter 可扩展

### 每帧流程

```
PumpEvents → GetDeltaTime → Input.Update → TickFrame(FixedStepAccumulator 固定步长累加 → FixedTick → Tick(活跃且未 Started 组件补发 OnStart, 仅一次) → LateTick)
→ RenderSystem.Render(Collector→Pipeline→RenderPacket→Submit 阻塞等GPU) → SceneManager.PostRender
→ CommitFrame(FrameSnapshotManager.CommitPending 销毁+注册+快照swap → drain FrameCommit → AssetManager 应用 AssetPipeline 结果 → AssetResidency/GPU release)
```

### 项目结构

```
src/SilkEngine.Runtime/            # Math/ Core/(EngineLog) Threading/ Input/ + Object.cs Time.cs
src/SilkEngine.Rendering.Abstraction/  # 无资产语义渲染契约（RenderPacket/Handle/IRenderable/ICameraView…）
src/SilkEngine.Rendering.Backend/  # 后端能力契约（IRenderBackend/IRenderDevice/IWindowSurface…）
src/SilkEngine.Assets/             # 资产域（AssetManager/AssetPipeline/Importer/Serialization/VFS/Binding + Render 域 Material*/MeshFactory）
src/SilkEngine.Scene/              # 场景域（GameObject/Component/SceneManager/FrameSnapshot/RendererBase/SceneRenderWorld…）
src/SilkEngine.Rendering/          # RenderSystem/RenderThreadHost/HeadlessRenderBackend + Pipeline/（internal Collector/ForwardPipeline）
src/SilkEngine.Rendering.OpenGL/   # OpenGL 后端 + DefaultWindowOption
src/SilkEngine.Host/               # 组合根：EngineHost/EngineBuilder/EngineOptions + internal EngineLoop
src/SilkEngine.SourceGen/          # [Service] 自动注册生成器 (netstandard2.0 Roslyn 增量生成器)
src/Sandbox/                       # 演示程序（Program.cs 逐个启用 + Demos/ 全部经 EngineHost+DemoAssetsExt + Gameplay.cs）
tests/SilkEngine.Tests/            # 560 个 xUnit 测试（含 Architecture/ 依赖边界测试）
tests/SilkEngine.SourceGen.Tests/  # 8 个 Service 注册测试
```

## 测试

- 框架: xUnit 2.9.3，目标 net10.0
- 568 个测试（SilkEngine.Tests 560 + SourceGen.Tests 8）覆盖 Math / Scene / Threading / Input / Render / Core / MeshFactory / Assets / Architecture（程序集依赖边界）/ Host
- TDD 强制: 所有业务逻辑代码必须先写测试→失败→实现→通过
- 测试文件按模块分目录: Math/ Scene/ Threading/ Input/ Render/ Core/（Assets 位于 Core/Assets）Architecture/（边界与纯净性断言）Host/ + SilkEngine.SourceGen.Tests（Service 注册测试）
- 验证基线: `dotnet test SilkEngine.slnx --settings seq.runsettings`（顺序模式规避既有 flaky）

## 约束

- 编辑器中只有 Safe 代码块可编译；ArbitraryCode 要求 `unsafe` 明确标注
- 遵照 engines 的规则：不跨工作区外引用文件；不允许直接复制 DEBUG 参考代码
- 不使用 `dynamic` / `System.Reflection` 来绕过类型安全
- 修改 ≥3 文件必须先获得用户授权 (RULE.md)
- Spec/Design/Plan 文档不提交 Git (RULE.md)
- Sensitive: 输入 API keys, secrets, or tokens 不可进入代码

## 已知问题 / 待办

### P0
- 渲染为主线程同步阻塞 (SubmitFrame 等 GPU)
- InstancedDrawCommand 无消费端
- Vulkan 后端为桩

### P1
- RenderPass.Filter 已定义未接线
- Transform.Scale 不组合父级
- SetParent 无环检测
- Instantiate 组件按默认值重建（不复刻状态；ComponentFactory 未注册类型静默跳过）
- 场景卸载只发 OnDestroy 不发 OnDisable
- Sprite/图片渲染组件（SpriteRenderer）与 UI 系统（Canvas/RawImage）待建；当前图片显示仅经 Material+MeshRenderer（TestPNGQuad 模式）
- FindEntry 线性扫描（规模增长需反向索引）
- Log 写入者全局共享的测试并行竞争（既有 flaky）
- 既有 FrameSnapshotTests 零分配测试的并行 flaky
- OpenGLMaterial 同名 uniform 覆盖风险
- 预留未接线: IComputeShader, RenderPacket, DrawIndirect
