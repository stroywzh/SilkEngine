# TODO List

> 2026-11-08 按代码核查重写：只保留未完成项；已实现/已过时的条目已删除，历史阶段计划不再引用。

## P0 阻断

- 测试/AssetLoadTests.cs : 第 27 行 `AssetRuntime.LoadAsync<Texture>(path)` 补 `CancellationToken.None`（当前 CS7036，测试工程编译不过）
- 主循环/MainThreadSynchronizationContext.cs、MainLoop.cs : 确认 `_mainThreadId` 捕获线程与 `Pump()` 调用线程一致；用 `--frames 200` 实测日志是否出现 “Pump 必须在主线程调用” 或提前停工
- 资产门面/Assets.cs、Core/AssetSystem/AssetRuntime.cs : 提供卸载入口（公开 `Unload` 或最小场景生命周期）；当前 `Unload` 为 internal 且无调用方

## 桥接

- Rendering/Assets/AssetRuntimeCache.cs : 给 `Drain` 加每帧上传预算（个数/字节），当前无上限
- Rendering/Assets/AssetRuntimeCache.cs : 永久失败后允许恢复（重导入/重新 ready 时清 pending），当前仅告警且不再重试
- Rendering/Assets/IAssetUploader.cs、AssetRuntimeCache.cs : 定扩展策略（保持 internal / 改 public / 换类型描述符）；当前只有 `TextureUploader`，`AddUploader` 为 internal
- Rendering/Assets/ : 加 `MeshUploader` + `MeshBinding`（VB/IB 句柄所有权归 binding）
- Rendering/Assets/AssetRuntimeCache.cs : 评估 AssetId→槽位/句柄表，替代每帧字典查找与 `_pending.Contains` 线性扫描（有 1000 draw 基线后再做）

## 资产系统

- Core/AssetSystem/AssetRuntime.cs、AssetDispatcher.cs : 背压检查先读 `QueuedCount` 再入队（非原子，且不含在飞解码）；改为原子计数并计入 in-flight
- Runtime/Host/AssetPump.cs : `Shutdown` 只 Cancel/Detach，未 Dispose `SemaphoreSlim` 与 CTS；重复启停会泄漏

## 测试

- tests/SilkEngine.Asset.Tests/AssetRuntimeCacheTests.cs : 补 RetryableFailure→重试成功用例（现仅覆盖永久失败）
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 pin 释放后同路径重新加载 → `TryPin` 复活用例
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 `ProcessDue` 延迟释放窗口（1000ms）用例
- tests/SilkEngine.Asset.Tests/ : 补 Shutdown 竞态用例（渲染线程先停、资产泵后停）
- tests/SilkEngine.Asset.Tests/ : 补 `Assets` 门面端到端用例（LoadAsync → ready → upload → unload）

## Mesh / 材质

- SilkEngine/Mesh.cs、Asset/Payload/MeshPayload.cs : 接 loader（仿 `TextureAssetLoader`）；当前无生产/消费
- SilkEngine.Asset/ : 加 obj/mtl importer + catalog adapter（当前 importer 只注册 PNG、catalog adapter 只注册纹理，`Cube.obj` 因无 sidecar 被跳过）
- Rendering/RenderCommands/ : 加 `DrawIndexed` 命令（设备已有 `DrawIndexed`；`BindIndexBufferCommand` 已定义但从未录制）
- Rendering/Abstraction/Handles.cs、RenderResources.cs : `MeshHandle`/`MaterialHandle` 只有声明无创建/销毁；设备资源改走资产句柄（原 TODO 未完成）
- Rendering/BuiltIn/BuiltInPipeline.cs : 加 Mesh 消费分支；当前只认 `TextureBinding`
- SilkEngine.Rendering/ : 材质（PSO）资产化：`CreatePipelineState` 已有，缺资产模型与命令

## 内容 / 身份

- Core/AssetSystem/AssetStore.cs : 运行时仍按 `ProjectPath` 建表；接 GUID→AssetId 映射（内容层 `AssetGuid` 已有）
- SilkEngine.Asset/ : 场景/预制体持久化引用（存 GUID、运行时解析）；GUID 目前只到内容层
- SilkEngine.Rendering/BuiltIn/OpenGLDevice.cs : sRGB 内部格式（当前固定 RGBA8，`ColorSpace` 仅标记）
- SilkEngine.Asset/Decoder/Png/ : chunk 数量无上限，补解码器硬化上限

## 暂缓（无需求或无第二实现，先不做）

- 多设备 : `AssetRuntime` 静态单例 + `AssetRuntimeCache` 按 `AssetId` 建键；无第二后端前不动
- 音频 : 全仓无 Audio/Sound/Wave/Ogg 实现
- 类型描述符收敛 : 等 2–3 种真实资产类型后再抽象

## 对象系统（旧待定，现状未验证）

- Core/ObjectSystem/、Scripting/ : 定 ObjectId 分配方式、Manager/Container 职责边界、Update 驱动接线
