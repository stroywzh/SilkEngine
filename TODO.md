# TODO List
- 1. GameObject和创建逻辑：ObjectId直接放在GameObject里面，有些东西还需要继续研究一下，这会先留着下面
  - EngineObjectManager负责上层管理：
  >- 1. （实例化）：分配ObjectId，驱动GameObject到Container的注册————或者干脆Container专门做几个静态门面（反正分成两种，Normal/DontDestoryOnLoad），把所有的数据存储到自身这里，Manager只负责。（待定，只能确定数据存储肯定要分开的，ObjectId要怎么分配，通过工厂还是啥都不确定）
  >- 2. （更新）：驱动UpdateDriver的Update/Late/Fixed~（待定，也可能之后就直接内聚更新逻辑到Manager了）
  >- 3. （接线）：Manager自身直接Instance出去还是通过接口（待定）

- 2. Rendering推进
  - 前置是基础的资源管理 Virtural Asset Table
  - 下面这张路径图按“**先做收益最大、风险最小的一步，每一步都有可运行的中间状态**”来排序。先给硬约束编号，后面每步引用编号，避免反复解释。
  - **进度（2026-10-30）：M1「资源导入 / 运行时资产系统」已完成**。链路：源 PNG → 导入器生成同名 `<source>.asset`（JSON schema v1）→ 主循环 `AssetSystem` 经 `UpdateDriver` 加载并分配 `AssetId` → 渲染线程按 `AssetId` 查只读快照上传纹理、经 `RenderCommand` 绘制（SandBox `--frames` 演示）。
    - 已完成：sidecar 幂等导入（哈希 + 原子写）；`Assets` 门面（`Load`/`LoadAsync` 占位/`TryGet`×2/`TryTakeReady`/`TryTakeReleased`/`Unload`/`PendingCount`）；`AssetTable` 无锁只读快照；引用计数 + 延迟释放（`DeferredReleaseFrames=1`）；加载器注册表（`Application.AssetLoaders`，Type→Loader，启动后只读）；主线程帧级异常降级；`BuiltInPipeline` 纹理路径（无纹理时回退 DEBUG 三角形）；PNG 解码器内部化 + 资源上限加固。
    - 验证：构建 0 error / 0 warning；33 个测试全绿；`dotnet run --project src/SandBox/SandBox.csproj -- --frames 180` exit 0（日志含导入/加载/上传/帧完成）。证据：`docs/rd/2026-10-30-m1-verification-record.md`，设计与结果见 `docs/rd/2026-10-30-asset-import-rd.md`（§6.4、§11）。
  - **后续（M2）**：
    - `Unload` / GPU 回收语义：`Assets.Unload` 返回值（审计 F-08）；延迟释放窗口与渲染端释放通知缺少自动化用例；
    - 异步解码 / 独立资产线程：`LoadAsync<T>` 仍是占位 throw，主线程解码仍会掉帧（首帧加载约 151 ms）；
    - sRGB 纹理格式：当前只有 `RGBA32` + `ColorSpace=SRGB` 标记，未使用 sRGB 内部格式；
    - 资产 GUID / 稳定标识：现在以规范化路径作为身份，移动文件会重复加载；
    - 多设备：`AssetId` 跨设备稳定，各设备各自维护 `AssetId → TextureHandle` 缓存；
    - 其它审计遗留（F-03/F-04/F-05/F-07/F-11/F-12 等）见 RD §11.4「M2 backlog」。

- 2. Rendering推进前置：
- 
/*
/   TODO:
/       1- AssetThread负责唤起扫描Root和基础资产导入
/       2- AssetThread负责按照Application的设置，注册LogFile和其他
/       3-关于资产导入：AssetThread负责监听和唤起导入行为，具体导入由AssetSystem/AssetScanner（框架）和Asset.*的具体业务逻辑负责
/
*/

基础资产导入和加载到运行时的处理由AssetThread递交给AssetSystem完成。AssetThread负责所有的编排和转发到ThreadPool，唤起监听所有的Asset文件夹，唤起导入，唤起ReImport等待。
LogSystem的处理交给AssetThread还是MainThread，有多帧生成时AssetThread负责应该会好一点。

一、总原则
先修正确性，再扩能力。 当前架构的 bug（并发重复加载、单写者契约漏洞）不修，新类型一上来就踩雷，后面每加一种资产都在放大问题。
每阶段可独立交付、可回滚。 不要一次把 payload 抽象 + artifact + GUID 全上，那是一次大爆炸。每阶段做完，系统都是自洽可用的。
不破坏现有 Texture 路径。 现有业务 API（Assets.Load<T>）、AssetTable.TryGet(id, out RawDataContainer)、渲染侧消费 s_ready/s_released 的代码，在阶段 1、2 里应保持编译和行为兼容。破坏性变更集中到最后。
扩展点收敛。 现在每加一种类型要散落改五处（资产类、loader、sidecar、importer、启动注册），且注册是 Lazy 冻结的。长期要收敛成"一个描述符 + 一行注册"。

二、阶段 0：修正确性（前置，必做）
目标：让现有架构的并发与生命周期语义真正成立。
为什么先做：后面每一阶段都会引入"同一路径被多请求""释放与上传竞态""注册时机错乱"这类场景，现在不修，等于在流沙上盖楼。
要修的几件事（按优先级）：
并发重复加载：同一路径在同帧被 LoadAsync 两次（或解码未完成时又来一次），当前会在 _entries.Add(path) 处抛 ArgumentException，并在 AssetTable 里留下永久泄漏的 id。思路是在 owner 线程维护"正在解码的路径集合"，命中则把请求挂到已有请求上共享结果，而不是二次注册。
单写者契约边界：AssetTable.Register/Unregister 是 public，任意线程可调，直接破坏 AssetStore 的单写者假设。思路是收回 internal，或在入口断言调用线程是 owner 或 inline 模式。
AssetInlineMode 是进程级静态计数：并行测试时 A 测试进 inline，B 测试也会走 inline，甚至与运行中的 worker 同时写 AssetStore。思路是改成线程级/异步上下文级，并在进入时断言"没有活跃 worker"。
PendingCount 漏算 worker 本地等待队列：背压判断失真。思路是把"已出队未进池"的请求也计入。
背压检查是竞态的：Count >= capacity 再 Enqueue 不是原子。思路是原子计数入队/出队。
AssetThread 资源未 Dispose：信号量、取消源、事件句柄在 Shutdown 只 Cancel 不释放。思路是统一 Dispose。

验收：同路径并发加载不抛异常、无泄漏；并行测试下 inline 模式互不串扰；监控数字与实际负载一致；反复启停 worker 无 handle 泄漏。


三、阶段 1：payload 抽象（能力地基）
目标：把"payload 一定是字节"变成"payload 是任何实现了统一接口的东西"。
为什么现在做：这是"任意类型资产"的真正门槛。不做这步，网格、音频采样、结构化配置、场景图都只能要么序列化绕一圈（额外拷贝 + 额外解码），要么传空壳。做完这步，非字节资产才能进入 AssetTable 和运行时管理。
思路：
定义一个载荷接口，表达两件事：是否已驻留到目标设备（纯 CPU 资产恒为真，GPU 资产上传后为真）、如何释放（卸载时统一调用）。
现有字节容器实现该接口，保留类名和对外属性不变——这样现有 GPU 上传路径一行不改。
新增"类型化对象载荷"作为通用容器，承载 JsonDocument、音频采样、网格等纯 CPU 载荷；需要特殊资源管理的（如网格带 GPU buffer）可自定义实现。
把 AssetDraft 的载荷字段从字节改成该接口；AssetTable 内部存储从字节容器改成接口。
AssetTable 保留"按字节容器取出"的强类型入口（现有调用方兼容），同时新增"按任意载荷类型取出"的入口。
统一释放点：在 AssetTable.Unregister 里调用载荷的释放方法。注意顺序——必须先让资产从所有快照不可见，再释放载荷，最后发释放通知。现有 AssetStore.Release 的顺序恰好满足，不要调换。
关键决策：载荷与资产实例的关系允许两种模式并存——"分离模式"（资产是元数据句柄，数据在载荷里，适合 GPU 上传）和"自包含模式"（资产本身就是数据，载荷只是包装，适合纯 CPU 资产）。不要强行统一，两种都有存在价值。
验收：一个非字节资产（如 JSON 配置或音频采样）能完整走通 LoadAsync → 注册 → TryGetAs 取出 → 卸载释放，且 Texture 路径编译和行为不变。

四、阶段 2：artifact 层（导入与运行时解耦）
目标：导入期把 payload 物化成内容寻址的产物文件；运行时只读产物，不再重新解码源文件。
为什么现在做：这是回答"IO 资产 payload 怎么加载"的核心。当前运行时每次都从源文件重新解码（PNG 解码、音频解码），既慢又占内存峰值。做完这步，导入与运行时的职责才真正分开：导入期负责"贵"的解码，运行时只负责 IO。

思路：
新增一个"导入产物仓库"：按内容哈希寻址，写时原子落盘（临时文件 + 重命名），天然去重。
导入器在解码后，把产物写入仓库，把产物哈希（或路径）记进 sidecar。sidecar 的 schema 版本升一级，校验时要求产物引用非空。
运行时 loader 从"读源文件 + 解码"改成"读 sidecar 拿产物位置 + 读产物"。宽高、格式、色彩空间等元数据全部从 sidecar 拿。
增量导入：重导入判断里增加产物哈希比对——源哈希不变、设置不变、产物存在，则跳过。
旧 sidecar 的 schema 版本不匹配时，读取层直接抛，导入器的"读已有 sidecar"把异常视为"需重导入"，自动升级。不需要写迁移代码，重导入即迁移。
对三类资产的差异化处理：

字节即产物（纹理、音频 PCM、字体图集、二进制包）：产物就是最终字节，loader 纯 IO。

结构化产物（网格、场景、材质）：产物是序列化后的结构，运行时反序列化；或评估反序列化成本，小文件可不物化。

程序化资产：无导入期，loader 内生成，artifact 层与它无关。

可选优化：大产物用内存映射，避免整块拷进托管数组。注意生命周期——载荷释放后映射失效，GPU 上传必须在此之前完成。现有"先不可见、再释放、后通知"的顺序保证上传窗口足够。
验收：纹理运行时不跑 PNG 解码；源文件不变时重导入跳过；旧 sidecar 被自动升级；产物仓库内容寻址可去重。

五、阶段 3：稳定身份与持久化引用（Unity .meta 的完整形态）
目标：让资产拥有跨会话稳定的身份，支撑场景/预制体持久化引用。
为什么在 artifact 之后：GUID 要写进 sidecar，sidecar 的结构在阶段 2 刚升过级，此时再加字段一并处理，避免连续两次 schema 变更。且 artifact 产物本身就是按内容寻址的，GUID 与它配合才是完整的"身份 + 产物"模型。

思路：
sidecar 基类加稳定 GUID。导入时若已有 sidecar 且 GUID 非空则保留，否则生成——重命名源文件、改设置都不应断引用。
AssetTable 在注册时建立 GUID → 运行时 id 的映射（owner 线程写，快照读）。
业务侧提供"按 GUID 取运行时 id"的入口。场景反序列化只存 GUID，运行时解析成 id 再取实例。
这一步会触及"引用体系"，是破坏性变更，放最后做。
验收：重启后同一资产的 GUID 不变；场景引用能跨会话解析；重命名源文件不断引用。

六、阶段 4：类型系统收敛（降低扩展成本）
目标：把"每加一种类型改五处"收敛成"一个描述符 + 一行注册"。
为什么最后做：前几阶段在改能力，这阶段在改"添加能力的方式"。等资产类型有了 2~3 种真实案例（字节型、结构化、程序化各一），再抽象描述符才有依据，否则是拍脑袋设计。

思路：
定义资产类型描述符：绑定"资产类型、loader、sidecar 编解码器、导入器、扩展名"。
启动时统一注册到一张表，替代当前 Lazy 冻结的字典——顺便修掉"注册时机错了就静默不生效、运行时才抛 NotSupportedException"的问题。
AssetSystem 的 loader 解析改为查这张表。
新类型 = 实现三个接口 + 一行注册。
验收：新增一种资产只需新增文件 + 一行注册，不改 AssetSystem、Assets、AssetTable 任何代码。

七、伴随项（不单独成阶段，随阶段推进）
两份快照非原子发布：AssetTable 快照与 AssetStore 快照在注册/释放时顺序相反，极短窗口内可能不一致。短期内用注释明确"两份快照独立发布、不保证跨表原子"；长期若要消除，需合并发布点。
ExceptionDispatchInfo 名不副实：完成路径捕获后只取源异常，从未重抛。要么简化成直接存异常，要么在 pump 里真正用它重抛以获得干净栈。不影响正确性，随阶段 1 顺手整理。
完成回调的竞争静默：TrySetResult 等返回 false 时被静默忽略，可加一次调试日志以定位"谁抢先完成"。
PNG 解码的 chunk 数量无上限：单 chunk 有上限，但大量小 chunk 可造成 CPU 抖动。属解码器硬化，可独立推进。

