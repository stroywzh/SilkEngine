# SilkEngine

基于 Silk.NET 的通用游戏引擎原型，C# / .NET 10 / OpenGL 4.6。当前处于框架原型阶段。

## 代码风格

- 所有公共 API 使用 C# 现代语法（init-only 属性、主构造函数、集合表达式）
- 命名空间：`SilkEngine` 根，子模块 `.Math` / `.Render` / `.Threading` / `.InputSystem`
- 静态类模式：`Time` / `SceneManager` / `Input` / `Log` 为全局门面
- 线程通过 `ThreadFactory.CreateThread` 统一创建（禁止直接 `new Thread()`）
- `allow(ArbirtaryCode)` requires safe code blocks explicitly: 仅 "unsafe" 标为 unsafe；所有其他代码逻辑应为 safe
- Priority: automated testing exists with full coverage (126 xUnit tests)

## 架构

### 线程模型

```
主线程 (Heartbeat)
  ├─ Input.Update → LogicLoop.Tick → OnRender → SubmitFrame(阻塞等GPU) → LateTick
  │
  ├─ RenderThreadLoop → 渲染线程 (ManualResetEventSlim 握手)
  └─ EngineThreadPool(2 workers) → 后台工作线程 (ConcurrentQueue 三优先级)
```

### 核心子系统

- **EngineLoop**: 心跳提供者，计算 dt → 驱动 Input/Logic/Tick/渲染。支持 Pause 和 Embedded 模式
- **LogicLoop**: 固定步长累加器 + SceneManager 派发 (FixedTick/Tick/LateTick/ProcessDestroys)
- **Scene System**: Object → GameObject(内置Transform) → Component(Enabled生命周期) → MonoBehaviour(9虚方法)
- **Render**: Camera → MeshRenderer → DrawCommand → RenderThreadLoop → OpenGLRenderBackend(缓存+ExecuteFrame)
- **Input**: Input门面 → KeyboardState/MouseState(双缓冲) → IInputProvider → SilkInputProvider
- **Threading**: ThreadFactory + EngineThreadPool(IWorkerScheduler) + RenderThreadLoop
- **Math**: 自研 Mathf/Vector2/Vector3/Quaternion/Matrix4x4 (左手系, column-major)
- **Log**: Log.Info/Warn/Error/Debug + StackTree + ILogWriter 可扩展

### 每帧流程

```
PumpEvents → GetDeltaTime → Input.Update → LogicLoop.Tick
→ OnRender(收集MeshRenderer→构建DrawCommand→SubmitFrame阻塞等GPU)
→ LogicLoop.LateTick(PostRender)
```

### 项目结构

```
src/SilkEngine/        # 引擎类库 (54 .cs)
  Math/ Scene/ Render/ Thread/ Core/ Input/
src/Sandbox/              # 演示程序
tests/SilkEngine.Tests/ # 126 个 xUnit 测试
```

## 测试

- 框架: xUnit 2.9.3，目标 net10.0
- 126 个测试覆盖 Math / Scene / Threading / Input / Render / Log
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
- RenderSystem/RenderPass 孤儿代码
- Transform.Scale 不组合父级
- Camera 默认正交 (应为透视)
- 预留未接线: IComputeShader, RenderPacket, DrawIndirect
