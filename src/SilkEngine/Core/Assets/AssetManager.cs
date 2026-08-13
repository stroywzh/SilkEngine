using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SilkEngine.Core.Assets.Importer;
using SilkEngine.Threading;

namespace SilkEngine.Core.Assets;

/// <summary>资产门面：同步/异步加载、GUID 缓存、帧末完成拾取（主线程专用 API）</summary>
public static class AssetManager
{
    private static readonly AssetCache _cache = new();
    private static readonly ConcurrentQueue<AssetLoadResult> _completed = new();
    private static IWorkerScheduler _scheduler = new EngineThreadPool(2);

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
        if (mode == AsyncLoadMode.LazyAsync)
            throw new NotSupportedException("AsyncLoadMode.LazyAsync 由资产管线 Part 3 实现");
        var request = new AssetRequest<T>();
        if (entry.State == AssetState.Loading && entry.Pending is not null)
        {
            entry.Awaiters.Add(request);
            return request;
        }
        entry.State = AssetState.Loading;
        entry.Pending = request;
        entry.Data = null;
        ScheduleLoad(guid, path);
        return request;
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
    }

    /// <summary>测试用：替换调度器（null 恢复默认 EngineThreadPool(2)）</summary>
    internal static void SetSchedulerForTests(IWorkerScheduler? scheduler) =>
        _scheduler = scheduler ?? new EngineThreadPool(2);

    /// <summary>测试断言用：当前缓存</summary>
    internal static AssetCache Cache => _cache;

    private static void ScheduleLoad(Guid guid, string path) =>
        _scheduler.Schedule(async () =>
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

    private static void CompleteAwaiters(AssetEntry entry, IAsset? asset, Exception? error)
    {
        entry.Pending?.Complete(asset, error);
        entry.Pending = null;
        foreach (var awaiter in entry.Awaiters)
            awaiter.Complete(asset, error);
        entry.Awaiters.Clear();
    }
}
