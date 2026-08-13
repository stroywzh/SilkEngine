using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SilkEngine.Core.Assets.Importer;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine.Core.Assets;

/// <summary>资产门面：同步/异步加载、GUID 缓存、帧末完成拾取（主线程专用 API）</summary>
public static class AssetManager
{
    private static readonly AssetCache _cache = new();
    private static readonly ConcurrentQueue<AssetLoadResult> _completed = new();
    private static readonly ConcurrentQueue<Guid> _pendingUnload = new();
    private static readonly ConcurrentQueue<Guid> _unloadQueue = new();
    private static readonly ConcurrentDictionary<IAssetRequest, (Guid Guid, string Path)> _lazyPending = new();
    private static IWorkerScheduler? _scheduler;

    /// <summary>
    /// 路径 → 稳定 GUID（归一化：反斜杠→斜杠、统一小写；跨运行与平台确定性）
    /// </summary>
    public static Guid PathToGuid(string path)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash);
    }

    /// <summary>完全同步加载（主线程 IO+解码；原型期仅适用于小资产）。失败直接抛出</summary>
    public static T Load<T>(string path) where T : IAsset
    {
        var guid = PathToGuid(path);
        var hit = _cache.Find(guid);
        if (hit is { State: AssetState.Ready, Data: T ready })
            return ready;
        if (hit is { State: AssetState.Loading })
            throw new InvalidOperationException($"资产 {path} 正在异步加载中，同步 Load 不可用");
        var importer = ImporterFactory.Create(Path.GetExtension(path));
        var asset = importer.Import(File.ReadAllBytes(path));
        if (asset is not T typed)
            throw new InvalidOperationException($"资产 {path} 类型为 {asset.GetType().Name}，不是 {typeof(T).Name}");
        var entry = _cache.GetOrAdd(guid);
        entry.Data = typed;
        entry.State = AssetState.Ready;
        return typed;
    }

    /// <summary>
    /// 异步加载：缓存命中直接返回已完成请求；否则登记 Loading + 工作线程调度
    /// <br/>同一 GUID 加载中时合并等待者，不重复调度；Failed 条目再次调用视为重试
    /// </summary>
    public static AssetRequest<T> LoadAsync<T>(string path, AsyncLoadMode mode = AsyncLoadMode.NormalAsync)
        where T : IAsset
    {
        var guid = PathToGuid(path);
        var entry = _cache.GetOrAdd(guid);
        if (entry.State == AssetState.Ready)
        {
            if (entry.Data is not T typed)
                throw new InvalidOperationException(
                    $"资产 {path} 类型为 {entry.Data?.GetType().Name ?? "null"}，不是 {typeof(T).Name}");
            return AssetRequest<T>.Completed(typed);
        }
        var request = new AssetRequest<T>();
        if (entry.State == AssetState.Loading && entry.Pending is not null)
        {
            entry.Awaiters.Add(request);
            return request;
        }
        if (mode == AsyncLoadMode.LazyAsync)
        {
            entry.State = AssetState.Loading;
            entry.Pending = request;
            entry.Data = null;
            _lazyPending[request] = (guid, path);
            return request;
        }
        entry.State = AssetState.Loading;
        entry.Pending = request;
        entry.Data = null;
        ScheduleLoad(guid, path);
        return request;
    }

    /// <summary>
    /// LazyAsync 触发点：AssetRequest.Asset 首次访问调用。
    /// <br/>登记存在且条目仍由该请求持有（Loading + Pending 匹配）才真正调度，幂等去重
    /// </summary>
    internal static void TriggerLazy(IAssetRequest request)
    {
        if (!_lazyPending.TryRemove(request, out var pending))
            return;
        var entry = _cache.Find(pending.Guid);
        if (entry is null || entry.State != AssetState.Loading || !ReferenceEquals(entry.Pending, request))
            return;
        ScheduleLoad(pending.Guid, pending.Path);
    }

    /// <summary>
    /// 帧末完成拾取：填 Asset/Error → IsDone → 主线程唤醒全部续延
    /// <br/>由 EngineLoop 在 Part 3 挂接到 CommitPending 链路；测试直接调用以模拟帧末
    /// </summary>
    public static void ProcessCompleted()
    {
        while (_completed.TryDequeue(out var result))
        {
            var entry = _cache.Find(result.guid);
            if (entry is null)
                continue;
            if (result.error is not null)
            {
                entry.State = AssetState.Failed;
                entry.Data = null;
                CompleteAwaiters(entry, null, result.error);
                Log.Error($"[AssetManager] 资产加载失败 ({entry.Guid}): {result.error.Message}");
            }
            else
            {
                entry.State = AssetState.Ready;
                entry.Data = result.asset;
                CompleteAwaiters(entry, result.asset, null);
            }
        }

        // 帧末卸载复核：仅检查归零候选（RefCount==0 且未被同帧重新引用）
        while (_pendingUnload.TryDequeue(out var unloadGuid))
        {
            var entry = _cache.Find(unloadGuid);
            if (entry is null || entry.RefCount != 0 || entry.State != AssetState.Ready)
                continue;
            entry.State = AssetState.Unloaded;
            _unloadQueue.Enqueue(unloadGuid);
        }
    }

    /// <summary>测试用：替换调度器（null 恢复延迟默认——首次调度时才创建）</summary>
    internal static void SetSchedulerForTests(IWorkerScheduler? scheduler) =>
        _scheduler = scheduler;

    /// <summary>引擎接入：EngineLoop.Initialize 注入共享工作线程池（避免重复建池）</summary>
    internal static void SetScheduler(IWorkerScheduler scheduler) => _scheduler = scheduler;

    /// <summary>测试断言用：当前缓存</summary>
    internal static AssetCache Cache => _cache;

    private static void ScheduleLoad(Guid guid, string path)
    {
        var scheduler = _scheduler ??= new EngineThreadPool(2);
        scheduler.Schedule(async () =>
        {
            AssetLoadResult result;
            try
            {
                var raw = await File.ReadAllBytesAsync(path);
                var importer = ImporterFactory.Create(Path.GetExtension(path));
                result = new AssetLoadResult(guid, importer.Import(raw), null);
            }
            catch (Exception ex)
            {
                result = new AssetLoadResult(guid, null, ex);
            }
            _completed.Enqueue(result);
        });
    }

    private static void CompleteAwaiters(AssetEntry entry, IAsset? asset, Exception? error)
    {
        entry.Pending?.Complete(asset, error);
        entry.Pending = null;
        foreach (var awaiter in entry.Awaiters)
            awaiter.Complete(asset, error);
        entry.Awaiters.Clear();
    }

    /// <summary>
    /// 渲染线程帧首调用：处理待释放队列
    /// <br/>glRelease 为 null 时维持 Part 2 行为（Log 占位 + CPU 数据清引用）；
    /// 传委托时（渲染线程传 _backend.ReleaseTexture）对队列中 Texture2D 条目执行 GL 释放
    /// </summary>
    internal static void ProcessUnloadQueue(Action<Texture2D>? glRelease = null)
    {
        while (_unloadQueue.TryDequeue(out var guid))
        {
            var entry = _cache.Find(guid);
            if (entry is null || entry.State != AssetState.Unloaded)
                continue;
            if (entry.RefCount > 0)
            {
                // 释放前被重新引用：恢复 Ready，取消卸载
                entry.State = AssetState.Ready;
                continue;
            }
            if (glRelease is not null)
            {
                if (entry.Data is Texture2D tex)
                    glRelease(tex);
                entry.Data = null;
                continue;
            }
            Log.Info($"[AssetManager] Unload asset {guid} (GL release pending: Part 3)");
            entry.Data = null;
        }
    }

    /// <summary>
    /// 托管资产引用 +1；非托管实例（缓存中无条目）no-op 返回 false
    /// <br/>按实例引用查找条目（Shader/Material 重写了 Equals，禁止 == 语义比较）
    /// </summary>
    public static bool TryAddRef(IAsset asset)
    {
        var entry = FindEntry(asset);
        if (entry is null || entry.Data is null)
            return false;
        entry.RefCount++;
        return true;
    }

    /// <summary>
    /// 托管资产引用 -1（下限 0）；归零时入卸载候选队列（帧末 ProcessCompleted 复核）
    /// <br/>非托管实例或已归零条目返回 false
    /// </summary>
    public static bool TryRelease(IAsset asset)
    {
        var entry = FindEntry(asset);
        if (entry is null || entry.RefCount <= 0)
            return false;
        entry.RefCount--;
        if (entry.RefCount == 0)
        {
            _pendingUnload.Enqueue(entry.Guid);
            if (asset is Material material)
                material.NotifyDisposed();
        }
        return true;
    }

    /// <summary>用户 API：无主资产显式归还；不调用则常驻缓存</summary>
    public static void Release(IAsset asset) => TryRelease(asset);

    /// <summary>
    /// 赋值点自动计数：新值 +1、旧值 -1；同一实例赋值短路
    /// <br/>非 IAsset 类型或非托管实例透明 no-op（向后兼容）
    /// </summary>
    public static void SetTracked<T>(ref T field, T value)
        where T : class
    {
        if (ReferenceEquals(field, value))
            return;
        var old = field;
        field = value;
        if (old is IAsset oldAsset)
            TryRelease(oldAsset);
        if (value is IAsset newAsset)
            TryAddRef(newAsset);
    }

    /// <summary>按实例引用查询缓存条目 GUID；非托管资产（缓存无条目）返回 false</summary>
    internal static bool TryGetGuid(IAsset asset, out Guid guid)
    {
        var entry = FindEntry(asset);
        if (entry is not null)
        {
            guid = entry.Guid;
            return true;
        }
        guid = Guid.Empty;
        return false;
    }

    /// <summary>按实例引用查找缓存条目</summary>
    private static AssetEntry? FindEntry(IAsset asset)
    {
        foreach (var entry in _cache.All())
            if (ReferenceEquals(entry.Data, asset))
                return entry;
        return null;
    }
}
