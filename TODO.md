# TODO List

> 2026-11-08 按代码核查重写：只保留未完成项；已实现/已过时的条目已删除，历史阶段计划不再引用。

## P0 阻断

<!-- - 测试/AssetLoadTests.cs : 第 27 行 `AssetRuntime.LoadAsync<Texture>(path)` 补 `CancellationToken.None`（当前 CS7036，测试工程编译不过） -->
<!-- -  [x] （已完成） 主循环/MainThreadSynchronizationContext.cs、MainLoop.cs : 确认 `_mainThreadId` 捕获线程与 `Pump()` 调用线程一致；用 `--frames 200` 实测日志是否出现 “Pump 必须在主线程调用” 或提前停工 -->
- 资产门面/Assets.cs、Core/AssetSystem/AssetRuntime.cs : 提供卸载入口（公开 `Unload` 或最小场景生命周期）；当前 `Unload` 为 internal 且无调用方（*批注：Unload暂时不公开，等到场景生命周期确定和完善后才会接入*）

## 桥接

- Rendering/Assets/AssetRuntimeCache.cs : 给 `Drain` 加每帧上传预算（个数/字节），当前无上限
- Rendering/Assets/AssetRuntimeCache.cs : 永久失败后允许恢复（重导入/重新 ready 时清 pending），当前仅告警且不再重试
- Rendering/Assets/IAssetUploader.cs、AssetRuntimeCache.cs : 定扩展策略（保持 internal / 改 public / 换类型描述符）；当前只有 `TextureUploader`，`AddUploader` 为 internal
- Rendering/Assets/ : 加 `MeshUploader` + `MeshBinding`（VB/IB 句柄所有权归 binding）
- Rendering/Assets/AssetRuntimeCache.cs : 评估 AssetId→槽位/句柄表，替代每帧字典查找与 `_pending.Contains` 线性扫描（有 1000 draw 基线后再做）

## 资产系统

- Core/AssetSystem/AssetRuntime.cs、AssetDispatcher.cs : 背压检查先读 `QueuedCount` 再入队（非原子，且不含在飞解码）；改为原子计数并计入 in-flight（*批注*）
- Runtime/Host/AssetPump.cs : `Shutdown` 只 Cancel/Detach，未 Dispose `SemaphoreSlim` 与 CTS；重复启停会泄漏（*批注：游戏引擎默认不会运行时逻辑重启（不过可以允许数据热重载），一旦崩溃就视为程序退出。*）

 ## 测试
<!--
- tests/SilkEngine.Asset.Tests/AssetRuntimeCacheTests.cs : 补 RetryableFailure→重试成功用例（现仅覆盖永久失败）
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 pin 释放后同路径重新加载 → `TryPin` 复活用例
- tests/SilkEngine.Asset.Tests/AssetStoreTests.cs : 补 `ProcessDue` 延迟释放窗口（1000ms）用例
- tests/SilkEngine.Asset.Tests/ : 补 Shutdown 竞态用例（渲染线程先停、资产泵后停）
- tests/SilkEngine.Asset.Tests/ : 补 `Assets` 门面端到端用例（LoadAsync → ready → upload → unload） -->
- *批注：全部测试内容略过，tests内容已删除等候重写*

## Mesh / 材质

- SilkEngine/Mesh.cs、Asset/Payload/MeshPayload.cs : 接 loader（仿 `TextureAssetLoader`）；当前无生产/消费
- SilkEngine.Asset/ : 加 obj/mtl importer + catalog adapter（当前 importer 只注册 PNG、catalog adapter 只注册纹理，`Cube.obj` 因无 sidecar 被跳过）（*批注：等待下面的1和2完成后就会进行这一条的实现*）
- Rendering/RenderCommands/ : 加 `DrawIndexed` 命令（设备已有 `DrawIndexed`；`BindIndexBufferCommand` 已定义但从未录制）
- Rendering/Abstraction/Handles.cs、RenderResources.cs : `MeshHandle`/`MaterialHandle` 只有声明无创建/销毁；设备资源改走资产句柄（原 TODO 未完成）
- Rendering/BuiltIn/BuiltInPipeline.cs : 加 Mesh 消费分支；当前只认 `TextureBinding`
- SilkEngine.Rendering/ : 材质（PSO）资产化：`CreatePipelineState` 已有，缺资产模型与命令

## 内容 / 身份

- Core/AssetSystem/AssetStore.cs : 运行时仍按 `ProjectPath` 建表；接 GUID→AssetId 映射（内容层 `AssetGuid` 已有）（*批注：这是全流程中需要第一个进行实现的，通过Guid计算Hash，然后通过Hash来建表，匹配等*）
- SilkEngine.Asset/ : 场景/预制体持久化引用（存 GUID、运行时解析）；GUID 目前只到内容层（*批注：等候场景层生命周期*）
- SilkEngine.Rendering/BuiltIn/OpenGLDevice.cs : sRGB 内部格式（当前固定 RGBA8，`ColorSpace` 仅标记）
- SilkEngine.Asset/Decoder/Png/ : chunk 数量无上限，补解码器硬化上限

## 暂缓（无需求或无第二实现，先不做）
<!-- 
- 多设备 : `AssetRuntime` 静态单例 + `AssetRuntimeCache` 按 `AssetId` 建键；无第二后端前不动
- 音频 : 全仓无 Audio/Sound/Wave/Ogg 实现
- 类型描述符收敛 : 等 2–3 种真实资产类型后再抽象 -->
- *批注：游戏引擎不要求同时出现第二后端，目前先不做这方面的防御，实现功能之后再说*

## 对象系统（旧待定，现状未验证）

- Core/ObjectSystem/、Scripting/ : 定 ObjectId 分配方式、Manager/Container 职责边界、Update 驱动接线
- *批注：这方面的会合并到场景，和场景一起去写*

## 1
TODO: Asset里面针对缓存的Reuse使用的是string的Path，我感觉直接缓存GUID或者指定的Index会好很多，存储和比较string的Path的开销绝对比这两个要大不少。
涉及：AssetSource/AssetStore/AssetRuntime/Asset

## 2
TODO: 审查中发现，如AssetStore等加入了专门的ReleaseRenderRef方法，就是为了增减RenderRef带来的引用计数，但是这是没有必要的，RenderRef带来的引用计数应该是作用域LiefTime而不是AssetStore，但是从调用链来看，可能只是方法名称没有改

## 3
我感觉代码中加入了很多不必要的空检查
有的时候这些内容肯定是需要构造赋值的，空检查会导致这些逻辑过于冗杂。
就比如下面这个我改过后的PayloadLease，
internal sealed class PayloadLease : IDisposable
{
    private IAssetPayload _payload;

    private PayloadLease()
    {
        throw new InvalidOperationException();
    }

    internal PayloadLease(IAssetPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payload = payload;
    }
    （其他代码）
}
这种运行时结果是确定的，IAssetPayload一定非null，却在调用时增加了空检查，属于过度防御，我大致看了一下暂存区的代码，有一些内容毫无意义，就是为了检查而检查，请将他们退化到出问题就直接throw或者Log.Error/Log.Exception。
同时，我感觉Asset对应的地方也是重灾区，这些资产相关的，除非有明确的尝试和错误后回退，否则是不需要任何防御的，只要出问题就直接throw/Log.Error/Log.Exception而不是静默回退至默认。
（例子1）    internal static bool TryGet<T>(AssetId assetId, out T asset)
        where T : IAsset
    {
        if (Store.TryGet(assetId, out AssetView view) && view.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        asset = default!;
        return false;
    }
像这样的，只要能TryGet到那就一定非null，所有就没有任何必要继续获取后检测nullable，不过也要考虑这样的是不是也有过度防御的嫌疑

目前看到的有问题的地方：
IAssetCatalog/PayloadLease/AssetRenderBridge/AssetStore/IRenderAssetSource，整条传递链都被这些东西污染了，到处都在做防御，但是有很多地方的防御做的毫无意义。

这是一次静态代码审查，审查之后把这些地方先列一个表出来。列完表之后给出一套防止过度防御的代码约束给用户审查。