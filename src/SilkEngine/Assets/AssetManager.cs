using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SilkEngine.Assets.Importer;
using SilkEngine.Core;

namespace SilkEngine.Assets;

/// <summary>
/// 资产门面（主线程专用 API）：同步/异步加载、GUID 缓存、引用计数闭环、帧末完成拾取。
/// ctor 自注册进 Services（EngineLoop 创建）；帧末 ProcessCompleted 由 EngineLoop.CommitFrame 调用；
/// LazyAsync 首次 await/访问 Asset 即触发实际调度；卸载（RefCount==0 帧末迁移）经 AssetUnloaded 事件发布，
/// GPU 删除由消费方在渲染线程执行。
/// </summary>
public sealed class AssetManager
{
    private readonly ITaskScheduler _scheduler;
    private readonly AssetCache _cache = new();
    private readonly ConcurrentQueue<AssetLoadResult> _completed = new();
    private readonly ConcurrentQueue<Guid> _pendingUnload = new();
    private readonly ConcurrentQueue<Guid> _unloadQueue = new();
    private readonly ConcurrentDictionary<IAssetRequest, (Guid Guid, string Path)> _lazyPending =
        new();

    /// <summary>帧末资产迁移 Unloaded 时发布（主线程）；GPU 删除由消费方在渲染线程执行。</summary>
    internal event Action<IAsset>? AssetUnloaded;

    /// <summary>构造注入任务调度器（引擎运行时经 ThreadManager 申请的 WorkerPool 执行者转型；执行者生命周期归 ThreadManager）</summary>
    public AssetManager(ITaskScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        Services.Register(this);
    }

    /// <summary>路径 → 稳定 GUID（归一化：反斜杠→斜杠、统一小写；跨运行与平台确定性）。纯函数，保持静态。</summary>
    /// <param name="path">资产路径</param>
    /// <returns>稳定 GUID（MD5 哈希）</returns>
    public static Guid PathToGuid(string path)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash);
    }

    /// <summary>完全同步加载（主线程 IO+解码；原型期仅适用于小资产）。失败直接抛出。</summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="path">资产路径（反斜杠/大小写差异不影响 GUID 稳定）</param>
    /// <returns>已就绪的资产实例（并写入缓存）</returns>
    /// <exception cref="ArgumentException">path 为 null 或空白</exception>
    /// <exception cref="InvalidOperationException">资产正在异步加载中（同步 Load 不可用）；或缓存条目类型与 T 不符</exception>
    public T Load<T>(string path)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var guid = PathToGuid(path);
        var hit = _cache.Find(guid);
        if (hit is { State: AssetState.Ready, Data: T ready })
        {
            if (LogConfig.Assets)
                Log.Info($"[Assets] Cache hit '{path}'");
            return ready;
        }
        if (hit is { State: AssetState.Loading })
            throw new InvalidOperationException($"资产 {path} 正在异步加载中，同步 Load 不可用");
        var importer = ImporterFactory.Create(Path.GetExtension(path));
        var asset = importer.Import(File.ReadAllBytes(path), new ImportSettings { Path = path });
        if (asset is not T typed)
            throw new InvalidOperationException(
                $"资产 {path} 类型为 {asset.GetType().Name}，不是 {typeof(T).Name}"
            );
        var entry = _cache.GetOrAdd(guid);
        _cache.SetData(entry, typed);
        entry.State = AssetState.Ready;
        return typed;
    }

    /// <summary>
    /// 异步加载：缓存命中直接返回已完成请求；否则登记 Loading + 工作线程调度。
    /// 同一 GUID 加载中时合并等待者，不重复调度；Failed 条目再次调用视为重试；
    /// LazyAsync 登记不调度，首次 await/访问 Asset 才触发。
    /// </summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="path">资产路径</param>
    /// <param name="mode">加载模式（默认 NormalAsync）</param>
    /// <returns>可 await 的加载请求（帧末 ProcessCompleted 唤醒续延）</returns>
    /// <exception cref="ArgumentException">path 为 null 或空白</exception>
    /// <exception cref="InvalidOperationException">缓存条目已就绪但类型与 T 不符</exception>
    public AssetRequest<T> LoadAsync<T>(string path, AsyncLoadMode mode = AsyncLoadMode.NormalAsync)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var guid = PathToGuid(path);
        var entry = _cache.GetOrAdd(guid);
        if (entry.State == AssetState.Ready)
        {
            if (entry.Data is not T typed)
                throw new InvalidOperationException(
                    $"资产 {path} 类型为 {entry.Data?.GetType().Name ?? "null"}，不是 {typeof(T).Name}"
                );
            return AssetRequest<T>.Completed(typed);
        }
        var request = new AssetRequest<T> { Manager = this };
        if (entry.State == AssetState.Loading && entry.Pending is not null)
        {
            entry.Awaiters.Add(request);
            return request;
        }
        if (mode == AsyncLoadMode.LazyAsync)
        {
            entry.State = AssetState.Loading;
            entry.Pending = request;
            _cache.SetData(entry, null);
            _lazyPending[request] = (guid, path);
            return request;
        }
        entry.State = AssetState.Loading;
        entry.Pending = request;
        _cache.SetData(entry, null);
        ScheduleLoad(guid, path);
        return request;
    }

    /// <summary>
    /// LazyAsync 触发点：AssetRequest.Asset 首次访问调用。
    /// <br/>登记存在且条目仍由该请求持有（Loading + Pending 匹配）才真正调度，幂等去重
    /// </summary>
    internal void TriggerLazy(IAssetRequest request)
    {
        if (!_lazyPending.TryRemove(request, out var pending))
            return;
        var entry = _cache.Find(pending.Guid);
        if (
            entry is null
            || entry.State != AssetState.Loading
            || !ReferenceEquals(entry.Pending, request)
        )
            return;
        ScheduleLoad(pending.Guid, pending.Path);
    }

    /// <summary>
    /// 帧末完成拾取：填 Asset/Error → IsDone → 主线程唤醒全部续延
    /// <br/>由 EngineLoop 每帧末调用；测试直接调用以模拟帧末
    /// </summary>
    public void ProcessCompleted()
    {
        while (_completed.TryDequeue(out var result))
        {
            var entry = _cache.Find(result.Guid);
            if (entry is null)
                continue;
            if (result.Error is not null)
            {
                entry.State = AssetState.Failed;
                _cache.SetData(entry, null);
                CompleteAwaiters(entry, null, result.Error);
                Log.Error($"[AssetManager] 资产加载失败 ({entry.Guid}): {result.Error.Message}");
            }
            else
            {
                entry.State = AssetState.Ready;
                _cache.SetData(entry, result.Asset);
                CompleteAwaiters(entry, result.Asset, null);
                if (LogConfig.Assets)
                    Log.Info($"[Assets] Load completed '{result.Guid}'");
            }
        }

        // 帧末卸载复核：仅检查归零候选（RefCount==0 且未被同帧重新引用）
        while (_pendingUnload.TryDequeue(out var unloadGuid))
        {
            var entry = _cache.Find(unloadGuid);
            if (entry is null || entry.RefCount != 0 || entry.State != AssetState.Ready)
                continue;
            entry.State = AssetState.Unloaded;
            AssetUnloaded?.Invoke(entry.Data!);
            _unloadQueue.Enqueue(unloadGuid);
        }
    }

    /// <summary>
    /// 渲染线程帧首调用：处理待释放队列
    /// <br/>release 为 null 时维持占位行为（Log + CPU 数据清引用）；传委托时对队列中条目执行 GPU 释放（消费方负责类型分发）
    /// </summary>
    internal void ProcessUnloadQueue(Action<IAsset>? release = null)
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
            if (release is not null)
            {
                if (entry.Data is { } data)
                    release(data);
                _cache.SetData(entry, null);
                if (LogConfig.Assets)
                    Log.Info($"[Assets] Released '{guid}'");
                _cache.Remove(guid);
                continue;
            }
            if (LogConfig.Assets)
                Log.Info($"[Assets] Unloaded '{guid}'");
            _cache.SetData(entry, null);
            _cache.Remove(guid);
        }
    }

    /// <summary>托管资产引用 +1；非托管实例（缓存中无条目）no-op 返回 false。</summary>
    /// <param name="asset">资产实例（按引用查找条目；Shader/Material 重写 Equals 禁止 == 比较）</param>
    /// <returns>引用计数递增成功为 true</returns>
    public bool TryAddRef(IAsset asset)
    {
        var entry = FindEntry(asset);
        if (entry is null || entry.Data is null)
            return false;
        entry.RefCount++;
        return true;
    }

    /// <summary>托管资产引用 −1（下限 0）；归零时入卸载候选队列（帧末 ProcessCompleted 复核），并触发 IReleaseAwareAsset 级联回调。</summary>
    /// <param name="asset">资产实例</param>
    /// <returns>引用递减成功为 true；非托管实例或已归零返回 false</returns>
    public bool TryRelease(IAsset asset)
    {
        var entry = FindEntry(asset);
        if (entry is null || entry.RefCount <= 0)
            return false;
        entry.RefCount--;
        if (entry.RefCount == 0)
        {
            _pendingUnload.Enqueue(entry.Guid);
            if (asset is IReleaseAwareAsset aware)
                aware.OnAssetReleased(this);
        }
        return true;
    }

    /// <summary>用户 API：无主资产显式归还（引用归零帧末迁移 Unloaded）；不调用则常驻缓存。</summary>
    /// <param name="asset">资产实例</param>
    public void Release(IAsset asset) => TryRelease(asset);

    /// <summary>赋值点自动计数：新值 +1、旧值 −1；同一实例赋值短路。非 IAsset 类型或非托管实例透明 no-op（向后兼容）。</summary>
    /// <typeparam name="T">字段类型</typeparam>
    /// <param name="field">被赋值字段（ref）</param>
    /// <param name="value">新值</param>
    public void SetTracked<T>(ref T field, T value)
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

    /// <summary>
    /// 引擎内部引用计数赋值桥：管理器已注册（引擎运行时）→ 计数闭环；
    /// 未注册（引擎初始化前/纯数据场景）→ 仅字段赋值，保持"非托管资产 no-op"等价语义
    /// </summary>
    internal static void SetTrackedAmbient<T>(ref T field, T value)
        where T : class
    {
        if (Services.TryGet<AssetManager>(out var manager))
            manager.SetTracked(ref field, value);
        else
            field = value;
    }

    /// <summary>按实例引用查询缓存条目 GUID；非托管资产（缓存无条目）返回 false</summary>
    internal bool TryGetGuid(IAsset asset, out Guid guid)
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

    /// <summary>测试断言用：当前缓存</summary>
    internal AssetCache Cache => _cache;

    /// <summary>GUID → 缓存资产（Data 为 T 即返回）；未命中或类型不符返回 null。</summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="guid">资产 GUID</param>
    /// <returns>已就绪资产；未命中/类型不符为 null</returns>
    public T? TryResolve<T>(Guid guid)
        where T : class, IAsset
    {
        var entry = _cache.Find(guid);
        return entry is { Data: T asset } ? asset : null;
    }

    private void ScheduleLoad(Guid guid, string path)
    {
        if (LogConfig.Assets)
            Log.Info($"[Assets] Load started '{path}' (guid: {guid})");
        _scheduler.Submit(async ct =>
        {
            AssetLoadResult result;
            try
            {
                var raw = await File.ReadAllBytesAsync(path, ct);
                var importer = ImporterFactory.Create(Path.GetExtension(path));
                result = new AssetLoadResult(
                    guid,
                    importer.Import(raw, new ImportSettings { Path = path }),
                    null
                );
            }
            catch (Exception ex)
            {
                result = new AssetLoadResult(guid, null, ex);
            }
            _completed.Enqueue(result);
        });
    }

    private void CompleteAwaiters(AssetEntry entry, IAsset? asset, Exception? error)
    {
        entry.Pending?.Complete(asset, error);
        entry.Pending = null;
        foreach (var awaiter in entry.Awaiters)
            awaiter.Complete(asset, error);
        entry.Awaiters.Clear();
    }

    private AssetEntry? FindEntry(IAsset asset) => _cache.FindByAsset(asset);
}
