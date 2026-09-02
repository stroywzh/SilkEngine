using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Threading;

namespace SilkEngine.Assets;

/// <summary>
/// 路径解析与结果投递契约（AssetManager 经此使用管线能力；internal，不进入 IAssetPipeline 公开面）。
/// </summary>
internal interface IAssetKeyResolver
{
    /// <summary>解析逻辑路径为构建键（索引未命中抛详细 InvalidOperationException；目录路径拒绝）</summary>
    AssetBuildKey ResolveKey(string path);

    /// <summary>查询资产当前源修订（无记录时 0）</summary>
    ulong CurrentSourceRevision(AssetId assetId);

    /// <summary>使指定资产的已完成缓存作业失效并递增源修订</summary>
    void Invalidate(AssetId assetId);

    /// <summary>结果接收器（AssetManager 设置；成功结果经 FrameCommit 阶段投递）</summary>
    Action<AssetPipelineResult>? ResultSink { get; set; }
}

/// <summary>
/// 资产管线协调器：按 <see cref="AssetBuildKey"/> 在 in-flight 字典中合并请求（锁保护，兼容测试多线程）；
/// Worker 只执行 Read/Import/依赖解析/Validate，生成不可变 <see cref="AssetPipelineResult"/>；
/// 成功结果经 FrameCommit 投递给 <see cref="ResultSink"/>（AssetManager 应用）。
/// 依赖以 DFS active set 检测循环，失败携带依赖链；源/导入器修订不匹配时过期结果以
/// <see cref="AssetStaleResultException"/> 失败，不写入缓存。
/// 仅由 Host/AssetManager 内部使用（internal；业务经 AssetManager 门面）。
/// </summary>
internal sealed class AssetPipeline : IAssetPipeline, IAssetKeyResolver
{
    private readonly IAssetFileSystem _files;
    private readonly IVirtualFileIndex _index;
    private readonly AssetCatalog _catalog;
    private readonly AssetImporterRegistry _importers;
    private readonly IBackgroundScheduler _background;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly ThreadRuntime _runtime;
    private readonly Dictionary<AssetBuildKey, SharedJob> _inflight = new();
    private int _executionCount;

    /// <summary>创建资产管线</summary>
    /// <param name="files">资产文件服务</param>
    /// <param name="index">虚拟文件索引（AssetId → 源节点 → 路径解析）</param>
    /// <param name="catalog">资产目录（记录修订与身份）</param>
    /// <param name="importers">导入器注册表</param>
    /// <param name="background">Worker 后台调度器</param>
    /// <param name="mainThread">主线程派发器（结果 FrameCommit 投递）</param>
    /// <param name="runtime">线程运行时（安全操作域判定）</param>
    public AssetPipeline(
        IAssetFileSystem files,
        IVirtualFileIndex index,
        AssetCatalog catalog,
        AssetImporterRegistry importers,
        IBackgroundScheduler background,
        IMainThreadDispatcher mainThread,
        ThreadRuntime runtime)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _importers = importers ?? throw new ArgumentNullException(nameof(importers));
        _background = background ?? throw new ArgumentNullException(nameof(background));
        _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>结果接收器：成功结果经 FrameCommit 阶段投递（AssetManager 设置；Main 域执行）</summary>
    internal Action<AssetPipelineResult>? ResultSink { get; set; }

    Action<AssetPipelineResult>? IAssetKeyResolver.ResultSink
    {
        get => ResultSink;
        set => ResultSink = value;
    }

    /// <summary>已创建作业数量（测试断言用：同键去重只执行一次）</summary>
    internal int ExecutionCount => Volatile.Read(ref _executionCount);

    /// <summary>已登记目录记录数量（测试断言用）</summary>
    internal int CatalogCountForTests => _catalog.Count;

    /// <summary>启动扫描入口：将一次扫描结果应用到虚拟文件索引（不预加载任何 Payload）。</summary>
    /// <param name="scan">启动扫描结果</param>
    public void ApplyScan(ScanResult scan) => _index.Apply(scan);

    /// <summary>解析逻辑路径为构建键：规范化 → 索引查询（未命中/目录抛详细异常）→ 目录登记 → 键。</summary>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <returns>构建键</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录（详细消息）</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public AssetBuildKey ResolveKey(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = _files.Normalize(path);
        if (!_index.TryGet(normalized, out var node) || node is null)
        {
            throw new InvalidOperationException(
                $"Asset path '{path}' was normalized to '{normalized}', "
                + "but it is not present in the VFS index. "
                + "Complete the startup asset scan before loading assets.");
        }
        if (node.NodeType != VirtualNodeType.File)
            throw new InvalidOperationException($"Asset path '{normalized}' resolves to a directory, not a file.");
        var extension = Path.GetExtension(normalized);
        if (!_importers.TryGetAssetType(extension, out var assetType))
            throw new NotSupportedException($"No importer for extension '{extension}'");
        var record = _catalog.GetOrAdd(node.Id, assetType);
        return new AssetBuildKey(record.AssetId, assetType, record.SourceRevision, ImporterRevision: 1, "");
    }

    /// <summary>查询资产当前源修订（无记录时 0）。</summary>
    /// <param name="assetId">资产标识</param>
    /// <returns>当前源修订</returns>
    public ulong CurrentSourceRevision(AssetId assetId)
        => _catalog.TryGet(assetId, out var record) ? record.SourceRevision : 0UL;

    /// <summary>使指定资产的已缓存结果失效并递增源修订（下次请求重新构建；在途作业完成后按过期校验失败）</summary>
    /// <param name="assetId">资产标识</param>
    public void Invalidate(AssetId assetId)
    {
        lock (_inflight)
        {
            if (_catalog.TryGet(assetId, out var record))
                record.SourceRevision++;
            var completed = _inflight
                .Where(kv => kv.Key.AssetId == assetId && kv.Value.Completion.Task.IsCompleted)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in completed)
                _inflight.Remove(key);
        }
    }

    /// <inheritdoc />
    public AssetOperation<T> Request<T>(AssetBuildKey key, CancellationToken cancellationToken = default)
        where T : class, IAssetPayload
    {
        var job = GetOrStartJob(key, null);
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() => DetachConsumer(job));
        var mapped = MapJob<T>(job);
        return new AssetOperation<T>(key.AssetId, mapped, () => DetachConsumer(job), _mainThread, _runtime);
    }

    private Task<T> MapJob<T>(SharedJob job)
        where T : class, IAssetPayload
        => job.Completion.Task.ContinueWith(
            static (t, _) =>
            {
                if (t.IsFaulted)
                    throw t.Exception!.GetBaseException();
                if (t.IsCanceled)
                    throw new OperationCanceledException();
                return (T)t.Result.Payload!;
            },
            null,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private SharedJob GetOrStartJob(AssetBuildKey key, List<AssetBuildKey>? chain)
    {
        lock (_inflight)
        {
            if (chain is not null && chain.Contains(key))
                throw CycleException(chain, key);
            if (_inflight.TryGetValue(key, out var existing))
            {
                existing.Consumers++;
                return existing;
            }
            var job = new SharedJob(key);
            _inflight[key] = job;
            Interlocked.Increment(ref _executionCount);
            var childChain = chain is null
                ? new List<AssetBuildKey> { key }
                : new List<AssetBuildKey>(chain) { key };
            StartWorker(job, childChain);
            return job;
        }
    }

    private void StartWorker(SharedJob job, List<AssetBuildKey> chain)
    {
        _background.Run(async ct =>
        {
            try
            {
                var result = await BuildResultAsync(job, chain, ct).ConfigureAwait(false);
                job.Completion.TrySetResult(result);
                if (ResultSink is not null)
                    _mainThread.Post(MainThreadPhase.FrameCommit, () => ResultSink?.Invoke(result));
            }
            catch (Exception ex)
            {
                job.Completion.TrySetException(ex);
            }
        });
    }

    private async Task<AssetPipelineResult> BuildResultAsync(SharedJob job, List<AssetBuildKey> chain, CancellationToken ct)
    {
        var key = job.Key;
        if (!_catalog.TryGet(key.AssetId, out var record))
            throw new InvalidDataException($"Asset record for '{key.AssetId}' is missing in the catalog.");
        if (!_index.TryGet(record.SourceNodeId, out var node) || node is null)
            throw new FileNotFoundException($"Source node for asset '{key.AssetId}' is not present in the VFS index.", record.SourceNodeId.Value.ToString());
        var path = node.LogicalPath;
        var settings = new ImportSettings { Path = path };
        var importer = _importers.Resolve(key.AssetType, Path.GetExtension(path), settings);

        var source = await _files.ReadAsync(path).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var import = importer.Import(source, new AssetImportContext(path, settings));

        // TODO(task 5): 按 AssetImportDependency 逻辑路径解析依赖构建键（ResolveKey），
        // 启动依赖作业并恢复 DFS 循环检测；本任务仅把路径依赖随结果携带，不做解析。
        ValidateFreshness(key, import.ImporterRevision);
        return new AssetPipelineResult(
            key,
            import.Payload,
            import.Dependencies,
            AssetPipelineResultState.Succeeded,
            null);
    }

    private void ValidateFreshness(AssetBuildKey key, ulong importerRevision)
    {
        var currentRevision = _catalog.TryGet(key.AssetId, out var record) ? record.SourceRevision : 0UL;
        if (currentRevision != key.SourceRevision || importerRevision != key.ImporterRevision)
            throw new AssetStaleResultException(key);
    }

    private InvalidDataException CycleException(List<AssetBuildKey> chain, AssetBuildKey key)
        => new($"Dependency cycle detected: {string.Join(" -> ", chain.Select(KeyLabel).Append(KeyLabel(key)))}");

    private string KeyLabel(AssetBuildKey key)
    {
        if (_catalog.TryGet(key.AssetId, out var record)
            && _index.TryGet(record.SourceNodeId, out var node)
            && node is not null)
        {
            return node.LogicalPath;
        }
        return key.AssetId.Value.ToString();
    }

    private void DetachConsumer(SharedJob job)
    {
        lock (_inflight)
        {
            job.Consumers--;
            if (job.Consumers <= 0 && !job.Completion.Task.IsCompleted)
            {
                job.Completion.TrySetCanceled();
                _inflight.Remove(job.Key);
            }
        }
    }

    private sealed class SharedJob(AssetBuildKey key)
    {
        public AssetBuildKey Key { get; } = key;

        public TaskCompletionSource<AssetPipelineResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Consumers;
    }
}
