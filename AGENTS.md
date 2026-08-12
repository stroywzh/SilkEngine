# SilkEngine

基于 Silk.NET 的通用游戏引擎原型，C# / .NET 10 / OpenGL 4.6。当前处于框架原型阶段。

## 代码风格

- 所有公共 API 使用 C# 现代语法（init-only 属性、主构造函数、集合表达式）
- 命名空间：`SilkEngine` 根，子模块 `.Math` / `.Render` / `.Threading` / `.InputSystem`
- 静态类模式：`Time` / `SceneManager` / `Input` / `Log` 为全局门面
- 线程通过 `ThreadFactory.CreateThread` 统一创建（禁止直接 `new Thread()`）
- `allow(ArbirtaryCode)` requires safe code blocks explicitly: 仅 "unsafe" 标为 unsafe；所有其他代码逻辑应为 safe
- Priority: automated testing exists with full coverage (185 xUnit tests)

## 架构

### 线程模型

```
主线程 (Heartbeat)
  ├─ Input.Update → LogicLoop.Tick → RenderSystem.Render(SubmitFrame阻塞等GPU) → LogicLoop.LateTick → CommitPending(帧末提交)
  │
  ├─ RenderThreadLoop → 渲染线程 (ManualResetEventSlim 握手, 由 RenderSystem 持有)
  └─ EngineThreadPool(2 workers) → 后台工作线程 (ConcurrentQueue 三优先级)
```

### 核心子系统

- **EngineLoop**: 心跳提供者，计算 dt → 驱动 Input/Logic/Tick/渲染。支持 Pause 和 Embedded 模式
- **FrameSnapshot/ComponentRegistry**: 帧原子性核心。ComponentRegistry 类型索引注册表（持久化 ComponentGroup），FrameSnapshotManager 双缓冲快照，帧末 CommitPending 统一应用销毁/注册并 swap（零分配）。销毁幂等（`_destroyPending`/`_destroyed` 双标志），LoadScene 场景切换注销旧场景全部组件
- **LogicLoop**: 固定步长累加器 + 基于快照的 SceneManager 派发 (FixedTick/Tick/LateTick)
- **Scene System**: Object → GameObject(内置Transform) → Component(活跃状态机: `RecomputeActiveState` 单一真理源, OnEnable/OnDisable/OnDestroy 下沉至 Component, Enabled/IsActive/SetParent 三路幂等重放) → MonoBehaviour(OnAwake/OnStart/OnUpdate/OnFixedUpdate/OnLateUpdate/OnPostRender)。工厂 `InitializeComponent`(挂载→OnAwake→OnEnable→注册)，GO 层级活跃门控 `IsActiveInHierarchy` 级联通知，`Started` 标志位 Start 补发，`AddObjectToScene` 运行时增删
- **Render**: RenderSystem(顶层管理) → RenderCollector(收集) → IRenderPipeline/ForwardPipeline(策略) → RenderPass[] → RenderThreadLoop → IRenderBackend(ExecutePass+Present)。相机矩阵经 `SingleDrawCommand.ViewMatrix/ProjectionMatrix` 携带，后端按 uModel 同款模式上传，不突变 Material
- **Input**: Input门面 → KeyboardState/MouseState(双缓冲) → IInputProvider → SilkInputProvider
- **Threading**: ThreadFactory + EngineThreadPool(IWorkerScheduler) + RenderThreadLoop
- **Math**: 自研 Mathf/Vector2/Vector3/Quaternion/Matrix4x4 (左手系, 行主序约定; GL 上传 UniformMatrix4 transpose=true)
- **Log**: Log.Info/Warn/Error/Debug + StackTree + ILogWriter 可扩展

### 每帧流程

```
PumpEvents → GetDeltaTime → Input.Update → LogicLoop.Tick(活跃且未 Started 组件补发 OnStart, 仅一次)
→ RenderSystem.Render(Collector→Pipeline→SubmitFrame阻塞等GPU)
→ LogicLoop.LateTick(PostRender)
→ FrameSnapshotManager.CommitPending(销毁+注册+快照swap)
```

### 项目结构

```
src/SilkEngine/        # 引擎类库 (60 .cs)
  Math/ Scene/ Render/ Thread/ Core/ Input/
src/Sandbox/              # 演示程序
tests/SilkEngine.Tests/ # 185 个 xUnit 测试
```

## 测试

- 框架: xUnit 2.9.3，目标 net10.0
- 185 个测试覆盖 Math / Scene / Threading / Input / Render / Core / MeshFactory
- TDD 强制: 所有业务逻辑代码必须先写测试→失败→实现→通过
- 测试文件按模块分目录: Math/ Scene/ Threading/ Input/ Render/ Core/

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
- Instantiate 不克隆组件
- 场景卸载只发 OnDestroy 不发 OnDisable
- OpenGLMaterial 同名 uniform 覆盖风险
- 预留未接线: IComputeShader, RenderPacket, DrawIndirect
