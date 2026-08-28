using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>同步后台调度器：Run 内联执行工作（结果在 Submit 返回前完成；测试夹具）</summary>
internal sealed class SyncBackgroundScheduler : IBackgroundScheduler
{
    public IJobHandle Run(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
    {
        work(cancellationToken).GetAwaiter().GetResult();
        return new TaskJobHandle(Task.CompletedTask);
    }
}

/// <summary>资产管线测试夹具：内存文件系统 + 同步调度管线 + 空索引（测试夹具）</summary>
internal static class TestAssetPipeline
{
    /// <summary>创建自注册 AssetManager（管线内建内存文件系统与空索引；调用方负责 Unregister）</summary>
    /// <param name="files">资产文件服务（默认空内存文件系统）</param>
    /// <param name="seedIndex">索引种子回调（ApplyScan 前的索引装配）</param>
    /// <returns>可独立使用的 AssetManager 实例（已注册进 Services，兼容旧 ctor 自注册语义）</returns>
    public static AssetManager CreateManager(IAssetFileSystem? files = null, Action<IVirtualFileIndex>? seedIndex = null)
    {
        var ctx = CreateContext(files, seedIndex);
        return ctx.Manager;
    }

    /// <summary>创建自注册 AssetManager 上下文（含线程运行时与管线，供测试排空 FrameCommit）</summary>
    /// <param name="files">资产文件服务（默认空内存文件系统）</param>
    /// <param name="seedIndex">索引种子回调（ApplyScan 前的索引装配）</param>
    /// <returns>管理器上下文（Manager 已注册进 Services；调用方负责注销）</returns>
    public static ManagerContext CreateContext(IAssetFileSystem? files = null, Action<IVirtualFileIndex>? seedIndex = null)
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var index = new InMemoryVirtualFileIndex();
        seedIndex?.Invoke(index);
        var pipeline = new AssetPipeline(
            files ?? new InMemoryAssetFileSystem("Assets"),
            index,
            new AssetCatalog(),
            new AssetImporterRegistry(),
            new SyncBackgroundScheduler(),
            runtime.MainThread,
            runtime);
        var manager = new AssetManager(pipeline, runtime.MainThread, runtime);
        Services.Register(manager); // 兼容旧 ctor 自注册语义（AssetOperation/Asset.Load 静态门面依赖）
        return new ManagerContext(manager, runtime, pipeline);
    }
}

/// <summary>管理器测试上下文（测试夹具）</summary>
/// <param name="Manager">自注册的资产管理器</param>
/// <param name="Runtime">线程运行时（FrameCommit 排空用）</param>
/// <param name="Pipeline">资产管线</param>
public sealed record ManagerContext(AssetManager Manager, ThreadRuntime Runtime, AssetPipeline Pipeline);
