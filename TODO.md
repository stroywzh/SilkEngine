# TODO List

> 2026-11-08 按代码核查重写：只保留未完成项；已实现/已过时的条目已删除，历史阶段计划不再引用。
> 2026-11-25 复核：主循环同步上下文生命周期已修复；上传预算、失败恢复、请求背压（原子准入，计入 in-flight）与资产泵关停资源释放已完成，相关条目删除；路径按当前代码更新。

## P0 阻断

- 资产门面/Assets.cs、Core/AssetSystem/Runtime/AssetSystemHost.cs : 提供卸载入口（公开 `Unload` 或最小场景生命周期）；当前 `Unload` 为 internal 且无调用方（*批注：Unload暂时不公开，等到场景生命周期确定和完善后才会接入*）

## 桥接

- Rendering/Assets/IAssetUploader.cs、RenderAssetBindingTable.cs : 定扩展策略（保持 internal / 改 public / 换类型描述符）；当前只有 `TextureUploader`，扩展点为 internal 构造注入
- Rendering/Assets/ : 加 `MeshUploader` + `MeshBinding`（VB/IB 句柄所有权归 binding）
- Rendering/Assets/RenderAssetBindingTable.cs : `_handles`（AssetId→RenderHandle）已建立；`_pending.Contains` 仍是线性扫描，待 1000 draw 基线后评估

 ## 测试
<!--
- tests/SilkEngine.Asset.Tests/AssetRuntimeCacheTests.cs : 补 RetryableFailure→重试成功用例（现仅覆盖永久失败）
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 pin 释放后同路径重新加载 → `TryPin` 复活用例
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 `ProcessDue` 延迟释放窗口（1000ms）用例
- tests/SilkEngine.Asset.Tests/ : 补 Shutdown 竞态用例（渲染线程先停、资产泵后停）
- tests/SilkEngine.Asset.Tests/ : 补 `Assets` 门面端到端用例（LoadAsync → ready → upload → unload） -->
- *批注：全部测试内容略过，tests内容已删除等候重写*

## Mesh / 材质

- SilkEngine/Mesh.cs、Asset/Payload/MeshPayload.cs : 接 loader（仿 `TextureAssetLoader`）；当前无生产/消费（`Asset/Internal/MeshAssetLoader.cs` 仍是空占位）
- SilkEngine.Asset/ : 加 obj/mtl importer + catalog adapter（当前 importer 只注册 PNG、catalog adapter 只注册纹理，`Cube.obj` 因无 sidecar 被跳过）（*批注：等待下面的1和2完成后就会进行这一条的实现*）
- Rendering/RenderCommands/ : 加 `DrawIndexed` 命令（设备已有 `DrawIndexed`；`BindIndexBufferCommand` 已定义但从未录制）
- Rendering/Abstraction/Handles.cs、RenderResources.cs : `MeshHandle`/`MaterialHandle` 只有声明无创建/销毁；设备资源改走资产句柄（原 TODO 未完成）
- Rendering/BuiltIn/BuiltInPipeline.cs : 加 Mesh 消费分支；当前只认 `TextureBinding`
- SilkEngine.Rendering/ : 材质（PSO）资产化：`CreatePipelineState` 已有，缺资产模型与命令

## 内容 / 身份

- Core/AssetSystem/Runtime/AssetRecordStore.cs : 运行时仍按 `ProjectPath` 建表；接 GUID→AssetId 映射（内容层 `AssetGuid` 已有）（*批注：这是全流程中需要第一个进行实现的，通过Guid计算Hash，然后通过Hash来建表，匹配等*）
- SilkEngine.Asset/ : 场景/预制体持久化引用（存 GUID、运行时解析）；GUID 目前只到内容层（*批注：等候场景层生命周期*）
- SilkEngine.Rendering/BuiltIn/OpenGLDevice.cs : sRGB 内部格式（当前固定 RGBA8，`ColorSpace` 仅标记）
- SilkEngine.Asset/Decoder/Png/ : chunk 数量无上限（现有单 chunk 256 MiB 上限，无数量/总量上限），补解码器硬化上限

## 暂缓（无需求或无第二实现，先不做）
<!-- 
- 多设备 : `AssetSystemHost` 静态单例 + `RenderAssetBindingTable` 按 `AssetId` 建键；无第二后端前不动
- 音频 : 全仓无 Audio/Sound/Wave/Ogg 实现
- 类型描述符收敛 : 等 2–3 种真实资产类型后再抽象 -->
- *批注：游戏引擎不要求同时出现第二后端，目前先不做这方面的防御，实现功能之后再说*

## 对象系统（旧待定，现状未验证）

- Core/ObjectSystem/、Scripting/ : 定 ObjectId 分配方式、Manager/Container 职责边界、Update 驱动接线（现有 `ObjectManager`/`ObjectContainer`/`ObjectLifecycle`，`ManagerBase` 已自动注册到 `UpdateScheduler`）
- *批注：这方面的会合并到场景，和场景一起去写*

## 1
TODO: Asset里面针对缓存的Reuse使用的是string的Path，我感觉直接缓存GUID或者指定的Index会好很多，存储和比较string的Path的开销绝对比这两个要大不少。
涉及：AssetLoadSource/AssetRecordStore/AssetSystemHost/Asset

## 2
TODO: 审查中发现，如AssetRecordStore等加入了专门的ReleaseRenderRef方法，就是为了增减RenderRef带来的引用计数，但是这是没有必要的，RenderRef带来的引用计数应该是作用域LiefTime而不是AssetRecordStore，但是从调用链来看，可能只是方法名称没有改

## 3
> 2026-11-25：约束已追加到 `RULE.md` 的 D13；已清理 `AssetSystemHost`/`AssetRecordStore`/`AssetDraft`/`PayloadLease`/`AssetBase`/`AssetDispatcher`/`AssetRenderBridge`/`IRenderAssetSource` 的确认项。剩余待评估：`RenderAssetBindingTable` 内部构造/`Drain` 的参数校验、`AssetCatalog.TryGet(string)` 的静默 `false`、`RenderAssetBindingTable` 的静默 drop 计数。
