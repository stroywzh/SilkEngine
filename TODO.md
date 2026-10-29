# TODO List
- 1. GameObject和创建逻辑：ObjectId直接放在GameObject里面，有些东西还需要继续研究一下，这会先留着下面
  - EngineObjectManager负责上层管理：
  >- 1. （实例化）：分配ObjectId，驱动GameObject到Container的注册————或者干脆Container专门做几个静态门面（反正分成两种，Normal/DontDestoryOnLoad），把所有的数据存储到自身这里，Manager只负责。（待定，只能确定数据存储肯定要分开的，ObjectId要怎么分配，通过工厂还是啥都不确定）
  >- 2. （更新）：驱动UpdateDriver的Update/Late/Fixed~（待定，也可能之后就直接内聚更新逻辑到Manager了）
  >- 3. （接线）：Manager自身直接Instance出去还是通过接口（待定）

- 2. Rendering推进
  - 前置是基础的资源管理 Virtural Asset Table
  - 下面这张路径图按“**先做收益最大、风险最小的一步，每一步都有可运行的中间状态**”来排序。先给硬约束编号，后面每步引用编号，避免反复解释。

---

## 一、先给现有硬约束编号

| 编号 | 硬约束                                                                     | 代码位置                                                                  |
| ---- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| H1   | 线程亲和：所有生命周期和录制调用必须 owner 线程                            | `RenderLifecycleBase.EnsureOwner`、`RenderBackendBase.EnsureRecordThread` |
| H2   | 单线程录制：lease 属于租赁线程，store 单生产者                             | `CommandBufferLease`、`CommandBufferStore.Append`                         |
| H3   | 全局无锁池：`_freeStores` 是单一 `Stack`，`_pending` 是单一 `Queue`        | `CommandQueue`                                                            |
| H4   | 执行单线程：`Execute` 串行消费 `_pending` 后 `Present`                     | `CommandQueue.Execute`                                                    |
| H5   | 命令式录制：管线按顺序直接 `Record`，无依赖分析                            | `RenderPipelineBase.RecordCommands`                                       |
| H6   | per-draw 绑定：纹理/管线/顶点缓冲逐次绑定                                  | `IRenderDevice` 的 `Bind*`/`SetPipelineState`                             |
| H7   | 最小能力集：`RenderCapabilities` 只有 5 个布尔，GL 后端无 compute/indirect | `RenderResources.cs`、`OpenGLDevice`                                      |
| H8   | 空缓冲提交被拒绝                                                           | `CommandQueue.Submit`                                                     |
| H9   | 静态 dispatch：虚方法 + 静态命令表，无运行时注册                           | `RenderBackendBase.Dispatch`                                              |

---

## 二、路径图（5 步，按推荐执行顺序）

### 第 1 步：FrameGraph 骨架 + `BuiltInPipeline` 迁移

**目标**：把“顺序录制命令”换成“声明 Pass → 编译依赖 → 执行录制”。这是唯一一步**完全不触碰线程、池、设备**的改造，风险最低、收益最大。

**新增文件**（建议目录 `Rendering/FrameGraph/`）：

| 文件                        | 职责                                                                       |
| --------------------------- | -------------------------------------------------------------------------- |
| `FrameGraphBuilder.cs`      | 声明式 API：`AddPass(reads, writes, setup, execute)`；每帧重建，纯 CPU     |
| `FrameGraphResource.cs`     | 资源句柄（不持有实际资源）+ 生命周期区间记录                               |
| `FrameGraphPass.cs`         | Pass 节点：读写集合、setup/execute 委托                                    |
| `FrameGraphCompiler.cs`     | 拓扑排序、无消费者 Pass 裁剪、资源生命周期分析、屏障决策占位               |
| `FrameGraphExecutor.cs`     | 遍历编译结果，通过 `ICommandBuffer.Record` 录制命令                        |
| `FrameGraphPipelineBase.cs` | 可选：继承 `RenderPipelineBase`，把 `RecordCommands` 默认路由到 FrameGraph |

**修改文件**：

| 文件                    | 改动                                                                                                                                          |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `RenderPipelineBase.cs` | 增加 `protected virtual bool UseFrameGraph => false`；`RecordFrame` 在 `true` 时走 FrameGraph 分支，`false` 保持旧路径（H5 的 fallback 开关） |
| `BuiltInPipeline.cs`    | 用 FrameGraph 重写：Viewport / ClearColor / Triangle 三个 Pass；旧 `RecordDebug*` 方法保留为 fallback                                         |
| `CommandQueue.cs`       | **不改**（FrameGraph 只在录制端工作，执行模型不变）                                                                                           |

**受影响的硬约束**：仅 H5（命令式录制）被替代，且旧路径保留。H1–H4、H6–H9 全部不动。

**验证**：`BuiltInPipeline` 的调试输出与改造前逐帧一致；`FrameGraphCompiler` 的 Pass 裁剪能正确剔除“输出未被消费”的实验性 Pass。

---

### 第 2 步（可与第 1 步并行）：能力集扩展 + 多线程录制准备

这一步建议**拆成 2a 和 2b**，2a 纯数据零风险，2b 涉及并发。

#### 2a：`RenderCapabilities` 扩展（纯数据）

**修改文件**：

| 文件                 | 改动                                                                                                                                                                       |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RenderResources.cs` | `RenderCapabilities` 增加 `SupportsCompute`、`SupportsIndirectDraw`、`SupportsStorageBuffers`、`SupportsBindlessTextures`、`SupportsMultiThreadRecording` 等构造参数和属性 |
| `OpenGLDevice.cs`    | `Capabilities` 返回新字段（GL 330 上 compute/indirect 置 `false`，作为 fallback 依据）                                                                                     |
| `IRenderDevice.cs`   | 无需改（能力通过 `Capabilities` 暴露即可）                                                                                                                                 |

**受影响硬约束**：H7（最小能力集）被扩展，但**不删除**任何现有能力。

#### 2b：多线程录制准备

**修改文件**：

| 文件                   | 改动                                                                                                                                                                                                                                                                  |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CommandBuffer.cs`     | `CommandBufferLease` 增加 `internal int RecordThreadId`，`Rent` 时写入；`Record` 入口校验调用线程 == `RecordThreadId`（H2 从“全局单线程”收紧为“lease 线程亲和”）                                                                                                      |
| `CommandQueue.cs`      | `_freeStores` 从 `Stack<CommandBufferStore>` 改为 `ConcurrentBag` 或 `ThreadLocal<Stack>`；`_pending` 从 `Queue` 改为 `ConcurrentQueue`；`Submit` 在合并入队时用 `Enqueue`（`ConcurrentQueue` 自身线程安全）；`_issued` 若跨线程访问需加锁或改 `ConcurrentDictionary` |
| `RenderBackendBase.cs` | `EnsureRecordThread` 保留默认 owner 检查，但 `RentCommandBuffer`/`Submit` 的校验改为“调用线程 == lease 的租赁线程”；派生类可覆盖为多线程允许                                                                                                                          |
| `ICommandBuffer.cs`    | 文档语义从“single-threaded”改为“lease 线程亲和；多个 lease 可来自不同线程”                                                                                                                                                                                            |
| `CommandBufferStore`   | **不改**（`Append` 的 `Array.Resize` 在单 store 内仍是单线程，安全）                                                                                                                                                                                                  |

**受影响硬约束**：H2（单线程录制）、H3（全局无锁池）。H1 的 owner 检查保留，但录制入口放宽。H4（执行单线程）**不动**——`_pending` 仍由 `Execute` 单线程消费。

**注意**：`ConcurrentQueue` 的 `Peek` 在 `Execute` 里要改成 `TryPeek`/`TryDequeue` 模式，避免并发下 `Peek` 与 `Dequeue` 之间的空档。建议先加 `SupportsMultiThreadRecording = false` 的开关，默认关闭，验证后再开。

---

### 第 3 步：资源表批量绑定（GL 版 bindless 语义）

**目标**：把 `BindTexture(slot, texture)` / `SetPipelineState` 从逐 draw 改为“每帧绑定一次材质表 + 实例表 + 纹理数组”。

**新增文件**（建议目录 `Rendering/ResourceTable/`）：

| 文件               | 职责                                                             |
| ------------------ | ---------------------------------------------------------------- |
| `MaterialTable.cs` | 材质数组的 CPU 侧描述 + 上传到 UBO/SSBO                          |
| `InstanceTable.cs` | 实例数组（变换、材质索引）                                       |
| `TextureTable.cs`  | 纹理数组或 bindless 句柄表（GL 4.6 `ARB_bindless_texture` 可选） |

**修改文件**：

| 文件                           | 改动                                                                                                                             |
| ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `IRenderDevice.cs`             | 增加 `SetMaterialTable(BufferHandle)`、`SetInstanceTable(BufferHandle)`、`SetTextureTable(...)`（或复用 `SetBindGroup` 11 命令） |
| `OpenGLDevice.cs`              | 实现资源表绑定；shader 通过 SSBO/纹理数组索引；保留逐 draw `BindTexture` 作为 fallback                                           |
| `RenderCommandKind.cs`         | 增加 `SetMaterialTable`、`SetInstanceTable`（或复用 `SetBindGroup` 的 id 11，用 payload 区分）                                   |
| `RenderResources.cs`           | 增加 `MaterialDesc`、`InstanceDesc` 结构                                                                                         |
| `RenderItem.cs`                | 可选：增加 `uint InstanceIndex`，与 `SortKey` 里的材质索引配合                                                                   |
| `BuiltInPipeline.cs` 或新 Pass | Opaque Pass 改为“绑定材质表 → 逐 item 只传 instance index”                                                                       |

**受影响硬约束**：H6（per-draw 绑定）、H7（能力集，若用 bindless 需 `SupportsBindlessTextures`）。

---

### 第 4 步：Indirect Draw

**目标**：Opaque Pass 不再逐 item 录制 `DrawIndexed`，而是 CPU 生成 `DrawElementsIndirectCommand[]`，只录制一条 `DrawIndexedIndirectCount`。

**新增文件**：

| 文件                                           | 职责                                                        |
| ---------------------------------------------- | ----------------------------------------------------------- |
| `Rendering/Indirect/IndirectCommandBuilder.cs` | 从 `RenderItem` 列表生成 indirect command 数组并上传 buffer |

**修改文件**：

| 文件                               | 改动                                                                                                                                                           |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IRenderDevice.cs`                 | 增加 `DrawIndexedIndirect(BufferHandle, int offset, int drawCount)` 和 `DrawIndexedIndirectCount(...)`                                                         |
| `OpenGLDevice.cs`                  | 用 `GL.DrawElementsIndirect` / `DrawElementsIndirectCount` 实现（GL 4.6 或 `ARB_indirect_parameters`）；能力不足时 `Capabilities.SupportsIndirectDraw = false` |
| `RenderResources.cs`               | 增加 `DrawIndexedIndirectCommand` 结构                                                                                                                         |
| `RenderCommandKind.cs`             | 已有 54/55，**无需改**；但 `RenderCommandTable` 需要注册这两个 executor                                                                                        |
| `BuiltInPipeline.cs` 或 OpaquePass | 用 `IndirectCommandBuilder` 替代逐 draw 录制                                                                                                                   |

**受影响硬约束**：H7（能力集）。H6 的“逐 draw 绑定”在此步已基本消失。

---

### 第 5 步：Compute 与 GPU 剔除（远期，建议先接 Vulkan 后端）

**目标**：GPU 侧完成视锥/遮挡剔除，`DrawIndexedIndirectCount` 的 count 由 compute shader 原子递增。

**新增/修改**：这一步改动面最大，建议新建后端而非改 GL：

- 新增 `Rendering/BuiltIn/VulkanDevice.cs`（或独立后端程序集）
- `IRenderDevice.cs` 增加 compute 相关（或拆出 `IComputeDevice` 可选接口）
- `RenderCapabilities` 的 `SupportsCompute`/`SupportsStorageBuffers` 在此步才真正置 `true`
- FrameGraph 新增 `ComputePass` 节点类型

**受影响硬约束**：H4（执行单线程——compute 提交可能引入多队列）、H7。建议此时才讨论多队列执行。

---

## 三、依赖关系与并行建议

```
第1步 FrameGraph ──────────────┐
                                ├──> 第3步 资源表 ──> 第4步 Indirect ──> 第5步 Compute
第2a步 能力扩展（可并行）──────┘
第2b步 多线程录制（可并行，风险高，建议第1步稳定后）
```

- **第 1 步与第 2a 步可完全并行**：FrameGraph 是纯 CPU，能力扩展是纯数据。
- **第 2b 步建议在第 1 步稳定后再做**：FrameGraph 在单线程下就能验证正确性，先确认图逻辑无误，再把录制线程放开。
- **第 3、4 步有严格依赖**：资源表是 indirect 的前提（indirect command 需要材质/实例索引）。
- **第 5 步不要在第 4 步之前启动**：GPU 剔除的收益依赖 indirect 发射端已经就绪。

---

## 四、文件变更总表

| 文件                    | 第1步                 | 第2a步         | 第2b步             | 第3步           | 第4步               |
| ----------------------- | --------------------- | -------------- | ------------------ | --------------- | ------------------- |
| `RenderPipelineBase.cs` | 改（加开关）          | —              | —                  | —               | —                   |
| `BuiltInPipeline.cs`    | 改（FrameGraph 重写） | —              | —                  | 改              | 改                  |
| `CommandBuffer.cs`      | —                     | —              | 改（线程 ID）      | —               | —                   |
| `CommandQueue.cs`       | —                     | —              | 改（并发池）       | —               | —                   |
| `RenderBackendBase.cs`  | —                     | —              | 改（录制线程钩子） | —               | —                   |
| `ICommandBuffer.cs`     | —                     | —              | 改（文档语义）     | —               | —                   |
| `RenderResources.cs`    | —                     | 改（能力扩展） | —                  | 改（材质/实例） | 改（indirect 结构） |
| `IRenderDevice.cs`      | —                     | —              | —                  | 改（资源表）    | 改（indirect）      |
| `OpenGLDevice.cs`       | —                     | 改（能力返回） | —                  | 改（资源表）    | 改（indirect）      |
| `RenderCommandKind.cs`  | —                     | —              | —                  | 改（新命令 id） | —                   |
| `RenderItem.cs`         | —                     | —              | —                  | 可选改          | 可选改              |
| `FrameGraph/*.cs`       | **新增 6 个**         | —              | —                  | —               | —                   |
| `ResourceTable/*.cs`    | —                     | —              | —                  | **新增 3 个**   | —                   |
| `Indirect/*.cs`         | —                     | —              | —                  | —               | **新增 1 个**       |

**关于“删”**：整条路径**不删除任何现有文件**。所有被替代的旧路径（`RecordDebug*`、逐 draw `BindTexture`、`_freeStores` 旧 `Stack`）都保留为 `Capabilities` 或开关控制的 fallback。这是工业化演进的基本原则：**每一步都能回退**。唯一“替换”的是 `CommandQueue._freeStores` 的类型和 `BuiltInPipeline` 的录制方式，但它们的行为在 fallback 开关下保持不变。

---

## 五、每步的完成判据（建议的验收点）

1. **第 1 步**：`BuiltInPipeline` 用 FrameGraph 重写后，逐帧像素输出与改造前一致；加一个未被消费的 Pass，确认它被 `FrameGraphCompiler` 裁剪掉且不执行。
2. **第 2a 步**：`OpenGLDevice.Capabilities` 新字段为 `false`，旧路径行为不变。
3. **第 2b 步**：用 4 个线程各录制 1000 条命令，`_pending` 合并后顺序确定；`ExecuteFrame` 输出与单线程录制一致。开关关闭时行为完全回退。
4. **第 3 步**：1000 个 `RenderItem` 的 draw 循环中，`BindTexture`/`SetPipelineState` 调用次数从 O(n) 降到 O(材质数/管线数)。
5. **第 4 步**：Opaque Pass 的 `ICommandBuffer.CommandsCount` 从 O(n) 降到常数（一条 indirect 命令）；`Capabilities.SupportsIndirectDraw = false` 时自动回退到第 3 步路径。