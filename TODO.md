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