using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Assets;

/// <summary>
/// 资产门面（主线程专用 API）：同步/异步加载、AssetId+源修订缓存、失效重载、引用计数闭环、帧末完成拾取。
/// ctor 自注册进 Services（EngineLoop 创建）；帧末 ProcessCompleted 由 EngineLoop.CommitFrame 调用；
/// LazyAsync 首次 await/访问 Asset 即触发实际调度；卸载（RefCount==0 帧末迁移）经 AssetUnloaded 事件发布，
/// GPU 删除由消费方在渲染线程执行。
/// 加载键为 AssetId + SourceRevision：后台结果携带调度时捕获的修订号，帧末与目录当前记录比较，
/// 过期结果丢弃并按当前修订重新调度；缓存命中只接受当前修订（Invalidate 使旧数据失效）。
/// 序列化器注册表经构造注入（实例级互不共享，无全局状态）；序列化层经 <see cref="Resolver"/> 视图解析已加载资产。
/// </summary>
public sealed class AssetManager : IDisposable
{
    private readonly IAssetFileSystem _files;
    private readonly IVirtualFileIndex? _index;
    private readonly AssetImporterRegistry _registry;
    private readonly ITaskScheduler _scheduler;
    private readonly IMainThreadDispatcher? _mainThread;
    private readonly ThreadRuntime? _runtime;
    private readonly AssetSerializerRegistry _serializerRegistry;
    private readonly AssetCatalog _catalog = new();
    private readonly AssetCache _cache = new();
    private readonly ConcurrentQueue<AssetLoadResult> _completed = new();
    private readonly ConcurrentQueue<AssetId> _pendingUnload = new();
    private readonly ConcurrentQueue<AssetId> _unloadQueue = new();
    private ulong _operationCounter;

    /// <summary>帧末资产迁移 Unloaded 时发布（主线程）；GPU 删除由消费方在渲染线程执行。</summary>
    internal event Action<IAsset>? AssetUnloaded;

    /// <summary>
    /// 受控引用解析器视图：按 AssetId 从本管理器缓存解析已加载资产（序列化层唯一资产访问边界，无全局服务定位）。
    /// 本管理器不持有序列化记录，<see cref="IAssetReferenceResolver.TryGetRecord"/> 恒返回 null。
    /// </summary>
    public IAssetReferenceResolver Resolver { get; }

    /// <summary>
    /// 构造注入文件服务、导入器注册表、任务调度器与序列化器注册表（引擎运行时经 ThreadManager 申请的 WorkerPool 执行者转型；
    /// 执行者生命周期归 ThreadManager）。构造即自注册进 Services。
    /// </summary>
    /// <param name="files">资产文件服务（逻辑路径只读访问）</param>
    /// <param name="registry">导入器注册表（扩展名 → 资产类型/导入器解析）</param>
    /// <param name="scheduler">后台加载任务调度器</param>
    /// <param name="serializerRegistry">序列化器注册表；null 时新建空注册表实例（实例级互不共享）</param>
    public AssetManager(
        IAssetFileSystem files,
        AssetImporterRegistry registry,
        ITaskScheduler scheduler,
        AssetSerializerRegistry? serializerRegistry = null,
        IMainThreadDispatcher? mainThread = null,
        ThreadRuntime? runtime = null)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _serializerRegistry = serializerRegistry ?? new AssetSerializerRegistry();
        _mainThread = mainThread;
        _runtime = runtime;
        Resolver = new CatalogReferenceResolver(this);
        Services.Register(this);
    }

    /// <summary>
    /// 严格索引构造：路径加载必须先经 <see cref="IVirtualFileIndex"/> 索引（启动扫描 Apply 后）；
    /// 未索引路径抛详细 InvalidOperationException，目录路径拒绝加载。旧四参构造保持宽松兼容（路径 MD5 身份），
    /// 由任务 5 统一删除。
    /// </summary>
    /// <param name="files">资产文件服务（逻辑路径只读访问）</param>
    /// <param name="index">虚拟文件索引（启动扫描结果；路径解析前置条件）</param>
    /// <param name="registry">导入器注册表（扩展名 → 资产类型/导入器解析）</param>
    /// <param name="scheduler">后台加载任务调度器</param>
    /// <param name="serializerRegistry">序列化器注册表；null 时新建空注册表实例（实例级互不共享）</param>
    public AssetManager(
        IAssetFileSystem files,
        IVirtualFileIndex index,
        AssetImporterRegistry registry,
        ITaskScheduler scheduler,
        AssetSerializerRegistry? serializerRegistry = null,
        IMainThreadDispatcher? mainThread = null,
        ThreadRuntime? runtime = null)
        : this(files, registry, scheduler, serializerRegistry, mainThread, runtime)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
    }

    /// <summary>启动扫描入口：将一次扫描结果应用到虚拟文件索引（不预加载任何 Payload）。</summary>
    /// <param name="scan">启动扫描结果</param>
    public void ApplyScan(ScanResult scan)
    {
        if (_index is null)
            throw new InvalidOperationException("此资产管理器未配置虚拟文件索引（宽松兼容模式）。");
        _index.Apply(scan);
    }

    /// <summary>注册序列化器（直通注册表；同类型重复注册抛 <see cref="InvalidOperationException"/>）</summary>
    /// <param name="serializer">待注册序列化器</param>
    public void RegisterSerializer(IAssetSerializer serializer) => _serializerRegistry.Register(serializer);

    /// <summary>
    /// 将外部任务包装为业务安全操作：不改变外部 Task 执行域，只把完成发布纳入 Main 安全阶段；
    /// 取消只影响本操作。经 <see cref="AssetOperation{T}.FromTask"/> 调用。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="task">外部任务</param>
    /// <returns>安全操作</returns>
    /// <exception cref="InvalidOperationException">管理器未装配主线程派发器/线程运行时</exception>
    internal AssetOperation<T> WrapExternalTask<T>(Task<T> task)
        where T : class, IAssetPayload
    {
        if (_mainThread is null || _runtime is null)
            throw new InvalidOperationException("资产管理器未装配主线程派发器与线程运行时，无法创建安全操作。");
        return new AssetOperation<T>(default, task, null, _mainThread, _runtime);
    }

    /// <summary>按类型与 schema 版本解析序列化器（直通注册表；未知类型或版本不支持抛 <see cref="NotSupportedException"/>）</summary>
    /// <param name="typeId">资产类型标识</param>
    /// <param name="schemaVersion">记录 schema 版本</param>
    /// <returns>匹配的序列化器</returns>
    public IAssetSerializer ResolveSerializer(AssetTypeId typeId, int schemaVersion)
        => _serializerRegistry.Resolve(typeId, schemaVersion);

    /// <summary>释放：注销服务定位器中的自注册（幂等；框架生命周期仍由 Services.Shutdown 反序管理）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    /// <summary>
    /// 完全同步加载（主线程 IO+解码；原型期仅适用于小资产）。失败直接抛出。
    /// </summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="path">资产逻辑路径（相对文件服务根目录；大小写/分隔符差异不影响键稳定）</param>
    /// <returns>已就绪的资产实例（并写入缓存）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    /// <exception cref="InvalidOperationException">资产正在异步加载中（同步 Load 不可用）；或缓存条目类型与 T 不符</exception>
    public T Load<T>(string path)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = _files.Normalize(path);
        var record = ResolveRecord(normalized, out _);
        var entry = _cache.Find(record.AssetId);
        if (
            entry is { State: AssetState.Ready, Data: T ready }
            && entry.SourceRevision == record.SourceRevision
        )
            return ready;
        if (entry is { State: AssetState.Loading })
            throw new InvalidOperationException($"资产 {path} 正在异步加载中，同步 Load 不可用");

        var importer = _registry.Resolve(
            record.AssetTypeId,
            Path.GetExtension(normalized),
            new ImportSettings { Path = normalized }
        );
        var raw = _files.ReadAsync(normalized).AsTask().GetAwaiter().GetResult();
        var asset = importer.Import(raw.ToArray(), new ImportSettings { Path = normalized });
        if (asset is not T typed)
            throw new InvalidOperationException(
                $"资产 {path} 类型为 {asset.GetType().Name}，不是 {typeof(T).Name}"
            );
        entry ??= _cache.GetOrAdd(record.AssetId);
        _cache.SetData(entry, typed);
        entry.State = AssetState.Ready;
        entry.SourceRevision = record.SourceRevision;
        return typed;
    }

    /// <summary>
    /// 异步加载（过渡期旧式请求 API，任务 5 切换为 <see cref="AssetOperation{T}"/>）：
    /// 缓存命中（条目 Ready 且源修订与目录一致）直接返回已完成请求；否则登记 Loading + 工作线程调度。
    /// 同一 AssetId 加载中时合并等待者，不重复调度；Invalidate 后旧修订数据视为未命中重新调度；
    /// Failed 条目再次调用视为重试。
    /// </summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <returns>可 await 的加载请求（帧末 ProcessCompleted 唤醒续延）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    /// <exception cref="InvalidOperationException">缓存条目已就绪且修订一致但类型与 T 不符</exception>
    public AssetRequest<T> LoadAsync<T>(string path)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = _files.Normalize(path);
        var record = ResolveRecord(normalized, out _);
        var entry = _cache.GetOrAdd(record.AssetId);
        if (entry.State == AssetState.Ready && entry.SourceRevision == record.SourceRevision)
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
        entry.State = AssetState.Loading;
        entry.Pending = request;
        _cache.SetData(entry, null);
        ScheduleLoad(entry, record, normalized);
        return request;
    }

    /// <summary>源变更失效：该路径目录记录修订号递增，旧修订缓存数据失效（下次访问重新加载）。</summary>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    public void Invalidate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = _files.Normalize(path);
        if (_index is not null)
        {
            if (_index.TryGet(normalized, out var node) && node is not null)
                _catalog.InvalidateSource(node.Id);
            return;
        }
        _catalog.InvalidateSource(NodeIdFromPath(normalized));
    }

    /// <summary>
    /// 帧末完成拾取：比较目录当前修订，过期结果丢弃（不写缓存、不唤醒续延）并按当前修订重新调度；
    /// 有效结果填 Asset/Error → IsDone → 主线程唤醒全部续延。
    /// <br/>由 EngineLoop 每帧末调用；测试直接调用以模拟帧末
    /// </summary>
    public void ProcessCompleted()
    {
        while (_completed.TryDequeue(out var result))
        {
            var entry = _cache.Find(result.AssetId);
            if (entry is null || result.OperationToken != entry.OperationToken)
                continue;
            if (!_catalog.TryGet(result.AssetId, out var record))
                continue;
            if (record.SourceRevision != result.SourceRevision)
            {
                // 过期结果：源已变更；仍有等待者时按当前修订重新调度
                if (entry.Pending is not null || entry.Awaiters.Count > 0)
                    ScheduleLoad(entry, record, entry.SourcePath!);
                continue;
            }
            if (result.Error is not null)
            {
                entry.State = AssetState.Failed;
                _cache.SetData(entry, null);
                CompleteAwaiters(entry, null, result.Error);
                Log.Error($"[AssetManager] 资产加载失败 ({entry.AssetId}): {result.Error.Message}");
            }
            else
            {
                entry.State = AssetState.Ready;
                entry.SourceRevision = result.SourceRevision;
                _cache.SetData(entry, result.Asset);
                CompleteAwaiters(entry, result.Asset, null);
                if (LogConfig.Assets)
                    Log.Info($"[Assets] Load completed '{entry.AssetId}'");
            }
        }

        // 帧末卸载复核：仅检查归零候选（RefCount==0 且未被同帧重新引用）
        while (_pendingUnload.TryDequeue(out var unloadAssetId))
        {
            var entry = _cache.Find(unloadAssetId);
            if (entry is null || entry.RefCount != 0 || entry.State != AssetState.Ready)
                continue;
            entry.State = AssetState.Unloaded;
            if (entry.Data is IAsset unloaded)
                AssetUnloaded?.Invoke(unloaded);
            _unloadQueue.Enqueue(unloadAssetId);
        }
    }

    /// <summary>
    /// 渲染线程帧首调用：处理待释放队列
    /// <br/>release 为 null 时维持占位行为（Log + CPU 数据清引用）；传委托时对队列中条目执行 GPU 释放（消费方负责类型分发）
    /// </summary>
    internal void ProcessUnloadQueue(Action<IAsset>? release = null)
    {
        while (_unloadQueue.TryDequeue(out var assetId))
        {
            var entry = _cache.Find(assetId);
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
                if (entry.Data is IAsset data)
                    release(data);
                _cache.SetData(entry, null);
                if (LogConfig.Assets)
                    Log.Info($"[Assets] Released '{assetId}'");
                _cache.Remove(assetId);
                continue;
            }
            if (LogConfig.Assets)
                Log.Info($"[Assets] Unloaded '{assetId}'");
            _cache.SetData(entry, null);
            _cache.Remove(assetId);
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
            _pendingUnload.Enqueue(entry.AssetId);
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

    /// <summary>按实例引用查询缓存条目资产 ID；非托管资产（缓存无条目）返回 false</summary>
    internal bool TryGetAssetId(IAsset asset, out AssetId assetId)
    {
        var entry = FindEntry(asset);
        if (entry is not null)
        {
            assetId = entry.AssetId;
            return true;
        }
        assetId = default;
        return false;
    }

    /// <summary>测试断言用：当前缓存</summary>
    internal AssetCache Cache => _cache;

    /// <summary>AssetId → 缓存资产（Data 为 T 且目录修订一致才返回）；未命中、类型不符或源已失效返回 null。</summary>
    /// <typeparam name="T">资产类型</typeparam>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已就绪资产；未命中/类型不符/失效为 null</returns>
    public T? TryResolve<T>(AssetId assetId)
        where T : class
        => TryResolveUntyped(assetId) as T;

    /// <summary>AssetId → 已就绪缓存资产（目录修订一致才返回）；未命中或源已失效返回 null。类型化查询与序列化层 resolver 视图共用。</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已就绪资产；未命中/失效为 null</returns>
    internal IAsset? TryResolveUntyped(AssetId assetId)
    {
        var entry = _cache.Find(assetId);
        if (entry is not { State: AssetState.Ready, Data: { } data })
            return null;
        // 缓存命中只接受当前修订：目录记录存在且修订不一致说明源已失效
        if (_catalog.TryGet(assetId, out var record) && entry.SourceRevision != record.SourceRevision)
            return null;
        return data as IAsset;
    }

    private AssetRecord ResolveRecord(string normalizedPath, out AssetTypeId assetTypeId)
    {
        if (_index is not null)
            return ResolveIndexed(normalizedPath, out assetTypeId);
        var extension = Path.GetExtension(normalizedPath);
        if (!_registry.TryGetAssetType(extension, out assetTypeId))
            throw new NotSupportedException($"No importer for extension '{extension}'");
        return _catalog.GetOrAdd(NodeIdFromPath(normalizedPath), assetTypeId);
    }

    /// <summary>严格索引解析：未命中抛详细 InvalidOperationException（不建节点/记录/不启动导入）；目录路径拒绝加载。</summary>
    private AssetRecord ResolveIndexed(string normalizedPath, out AssetTypeId assetTypeId)
    {
        if (!_index!.TryGet(normalizedPath, out var node) || node is null)
        {
            throw new InvalidOperationException(
                $"Asset path '{normalizedPath}' was normalized to '{normalizedPath}', "
                + "but it is not present in the VFS index. "
                + "Complete the startup asset scan before loading assets.");
        }
        if (node.NodeType != VirtualNodeType.File)
            throw new InvalidOperationException($"Asset path '{normalizedPath}' resolves to a directory, not a file.");
        var extension = Path.GetExtension(normalizedPath);
        if (!_registry.TryGetAssetType(extension, out assetTypeId))
            throw new NotSupportedException($"No importer for extension '{extension}'");
        return _catalog.GetOrAdd(node.Id, assetTypeId);
    }

    /// <summary>已登记目录记录数量（测试断言用）</summary>
    internal int CatalogCountForTests => _catalog.Count;

    private void ScheduleLoad(AssetEntry entry, AssetRecord record, string path)
    {
        var operationToken = ++_operationCounter;
        var sourceRevision = record.SourceRevision;
        entry.OperationToken = operationToken;
        entry.SourceRevision = sourceRevision;
        entry.SourcePath = path;
        if (LogConfig.Assets)
            Log.Info($"[Assets] Load started '{path}' (asset: {record.AssetId}, rev: {sourceRevision})");
        _scheduler.Submit(async ct =>
        {
            AssetLoadResult result;
            try
            {
                var raw = await _files.ReadAsync(path);
                var importer = _registry.Resolve(
                    record.AssetTypeId,
                    Path.GetExtension(path),
                    new ImportSettings { Path = path }
                );
                result = new AssetLoadResult(
                    record.AssetId,
                    sourceRevision,
                    operationToken,
                    importer.Import(raw.ToArray(), new ImportSettings { Path = path }),
                    null
                );
            }
            catch (Exception ex)
            {
                result = new AssetLoadResult(record.AssetId, sourceRevision, operationToken, null, ex);
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

    /// <summary>规范化逻辑路径 → 稳定虚拟节点 ID（MD5 哈希；跨运行与平台确定性）</summary>
    private static VirtualNodeId NodeIdFromPath(string normalizedPath)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return new VirtualNodeId(new Guid(hash));
    }

    /// <summary>
    /// 受控引用解析器视图：按 AssetId 从本管理器缓存解析已加载资产（未加载/未命中返回 null，调用方据此区分加载中）。
    /// 本管理器不持有序列化记录，TryGetRecord 恒返回 null；无任何全局服务定位或静态状态。
    /// </summary>
    private sealed class CatalogReferenceResolver(AssetManager manager) : IAssetReferenceResolver
    {
        /// <summary>本管理器不持有序列化记录，恒返回 null</summary>
        public AssetSerializationRecord? TryGetRecord(AssetId assetId) => null;

        /// <summary>按强类型句柄从缓存解析已加载资产；未加载/未命中返回 null</summary>
        public T Resolve<T>(AssetHandle<T> handle)
            where T : class
            => manager.TryResolveUntyped(handle.Id) as T ?? null!;

        /// <summary>按非泛型句柄从缓存解析已加载资产；未加载/未命中返回 null</summary>
        public object Resolve(UntypedAssetHandle handle) => manager.TryResolveUntyped(handle.Id) ?? null!;
    }
}
