# SilkEngine

基于 Silk.NET 的通用游戏引擎原型，C# / .NET 10 / OpenGL 4.6。当前处于框架原型阶段。

## 代码风格

- 所有公共 API 使用 C# 现代语法（init-only 属性、主构造函数、集合表达式）
- 命名空间：根命名空间不放置类型，全部归于子命名空间——`SilkEngine.Core`（含 `.Core.Assets`）/ `.Scene` / `.Render` / `.Threading` / `.InputSystem` / `.Math`
- 静态门面与实例模式：`Time` / `Input` / `Log` 为全局门面；`SceneManager` / `AssetManager` 为实例类（EngineLoop 创建并注册进 Services，跨程序集经 EngineLoop 公开属性取用）
- 线程通过 `ThreadFactory.CreateThread` 统一创建（禁止直接 `new Thread()`）
- `allow(ArbirtaryCode)` requires safe code blocks explicitly: 仅 "unsafe" 标为 unsafe；所有其他代码逻辑应为 safe
- Priority: automated testing exists with full coverage (422 xUnit tests)

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

- **Services**: `internal static class`（SilkEngine.Core）服务定位器：Register（重复注册抛错）/ Get（未注册 fail-fast）/ TryGet（初始化前静默回退，如 GameObject 注册回退链）/ Unregister（测试夹具用）/ Shutdown（反序 Dispose 全部 IDisposable 服务并清空注册表，幂等）。EngineLoop.Initialize 注册管理者实例，跨程序集经 EngineLoop 公开属性取用。`[Service(Priority, Name)]` 特性经 ServiceRegistrationGenerator 自动注册（ModuleInitializer，按 Priority 升序、类名次排序；仅引擎程序集 SERV001/002 把关）
- **EngineLoop**: 心跳提供者，计算 dt（钳制 0.1s）→ 驱动 Input/Tick/渲染。内建 FixedStepAccumulator（LogicLoop 合并，替代 LogicLoop.FixedDeltaTime）；`Initialize` 创建 RenderSystem/AssetManager 并 `Services.Register` 全部管理者、`SceneManager.Attach` 注入注册表与快照管理器；`CommitFrame` 私有帧末提交（销毁→注册→快照 swap→资产完成）；公开 `SceneManager`/`AssetManager` 属性；`Dispose → Services.Shutdown` 反序释放。支持 Pause 和 Embedded 模式
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
src/SilkEngine/              # 引擎类库 (92 .cs)
  Core/ (含 Assets/ + Assets/Importer/)  Scene/  Rendering/ (含 Abstraction/ Backend/ OpenGL/ Vulkan/ Pipeline/)
  Threading/  Input/  Math/
src/SilkEngine.SourceGen/    # [Service] 自动注册生成器 (netstandard2.0 Roslyn 增量生成器)
src/Sandbox/                 # 演示程序 (Program.cs 逐个启用 + Demos/ 9 文件共享 ShaderSources + Gameplay.cs; Resources/test.png)
tests/SilkEngine.Tests/      # 415 个 xUnit 测试
tests/SilkEngine.SourceGen.Tests/  # 7 个 Service 注册测试
```

## 测试

- 框架: xUnit 2.9.3，目标 net10.0
- 422 个测试（SilkEngine.Tests 415 + SourceGen.Tests 7）覆盖 Math / Scene / Threading / Input / Render / Core / MeshFactory / Assets
- TDD 强制: 所有业务逻辑代码必须先写测试→失败→实现→通过
- 测试文件按模块分目录: Math/ Scene/ Threading/ Input/ Render/ Core/（Assets 位于 Core/Assets）+ SilkEngine.SourceGen.Tests（Service 注册测试）

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
