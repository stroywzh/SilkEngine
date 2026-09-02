using SilkEngine.Assets.Database;
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
/// 资产管线协调器：按 <see cref="AssetBuildKey"/> 在 in-flight 字典中合并请求（锁保护，兼容测试多线程）。
/// Worker 执行 Read/Import/依赖解析/Validate：导入器返回的路径依赖被解析为依赖构建键并启动依赖作业
/// （DFS active set 检测循环，失败携带完整路径链）；依赖作业完成后父作业产出最终结果。
/// 成功结果经 FrameCommit 阶段先持久化依赖边（Dependencies 表 + 内存 AssetDependencyIndex，单事务），
/// 再投递给 <see cref="ResultSink"/>（AssetManager 应用）。
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
    private readonly IAssetDatabase? _database;
    private readonly object _databaseGate = new();
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
    /// <param name="database">资产数据库（可为 null：依赖边仅驻留内存反向索引）</param>
    public AssetPipeline(
        IAssetFileSystem files,
        IVirtualFileIndex index,
        AssetCatalog catalog,
        AssetImporterRegistry importers,
        IBackgroundScheduler background,
        IMainThreadDispatcher mainThread,
        ThreadRuntime runtime,
        IAssetDatabase? database = null)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _importers = importers ?? throw new ArgumentNullException(nameof(importers));
        _background = background ?? throw new ArgumentNullException(nameof(background));
        _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _database = database;
    }

    /// <summary>结果接收器：成功结果经 FrameCommit 阶段投递（AssetManager 设置；Main 域执行）</summary>
    internal Action<AssetPipelineResult>? ResultSink { get; set; }

    /// <summary>依赖索引：正向/反向内存索引（FrameCommit 阶段回写；测试断言用）</summary>
    internal AssetDependencyIndex DependencyIndex { get; } = new();

    /// <summary>资产数据库（构造注入；测试断言持久化用）</summary>
    internal IAssetDatabase? Database => _database;

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

    /// <summary>
    /// 解析逻辑路径为构建键：规范化 → 索引查询（未命中/目录抛详细异常）→ 目录登记 → 键（含导入设置指纹）。
    /// </summary>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <returns>构建键</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录（详细消息）</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public AssetBuildKey ResolveKey(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = _files.Normalize(path);
        return KeyForPath(normalized, expectedType: null);
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
                // FrameCommit 阶段（Main 域）：先回写依赖边（单事务 DB + 内存反向索引），再投递结果
                _mainThread.Post(MainThreadPhase.FrameCommit, () =>
                {
                    PersistDependencies(result);
                    ResultSink?.Invoke(result);
                });
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

        var resolved = await ResolveDependenciesAsync(import.Dependencies, chain, ct).ConfigureAwait(false);
        ValidateFreshness(key, import.ImporterRevision);
        return new AssetPipelineResult(
            key,
            HydratePayload(key, import.Payload, resolved),
            import.Dependencies,
            AssetPipelineResultState.Succeeded,
            null);
    }

    /// <summary>
    /// 解析导入器声明的路径依赖：逐条解析为依赖构建键（含期望类型校验），沿当前 DFS 链启动依赖作业并等待完成；
    /// 循环依赖经 <see cref="GetOrStartJob"/> 的链检查抛带完整路径链的 <see cref="InvalidDataException"/>。
    /// </summary>
    private async Task<IReadOnlyList<ResolvedDependency>> ResolveDependenciesAsync(
        IReadOnlyList<AssetImportDependency> dependencies,
        List<AssetBuildKey> chain,
        CancellationToken ct)
    {
        var resolved = new List<ResolvedDependency>(dependencies.Count);
        foreach (var dependency in dependencies)
        {
            var dependencyKey = ResolveDependencyKey(dependency);
            var dependencyJob = GetOrStartJob(dependencyKey, chain);
            try
            {
                var result = await dependencyJob.Completion.Task.ConfigureAwait(false);
                resolved.Add(new ResolvedDependency(dependency, result.Key.AssetId, dependencyKey.AssetType));
            }
            finally
            {
                DetachConsumer(dependencyJob);
            }
        }
        ct.ThrowIfCancellationRequested();
        return resolved;
    }

    /// <summary>按依赖声明解析构建键：规范化 → 索引查询 → 期望类型校验 → 目录登记 → 键（与 ResolveKey 同语义）</summary>
    private AssetBuildKey ResolveDependencyKey(AssetImportDependency dependency)
    {
        var normalized = _files.Normalize(dependency.LogicalPath);
        if (!_index.TryGet(normalized, out var node) || node is null)
        {
            throw new FileNotFoundException(
                $"Dependency path '{dependency.LogicalPath}' was normalized to '{normalized}', "
                + "but it is not present in the VFS index.",
                normalized);
        }
        if (node.NodeType != VirtualNodeType.File)
            throw new InvalidDataException($"Dependency path '{normalized}' resolves to a directory, not a file.");
        var extension = Path.GetExtension(normalized);
        if (!_importers.TryGetAssetType(extension, out var assetType))
            throw new NotSupportedException($"No importer for extension '{extension}'");
        if (dependency.ExpectedType is { } expected && expected != assetType)
        {
            throw new InvalidDataException(
                $"Dependency '{dependency.LogicalPath}' expects asset type '{expected.Value}', "
                + $"but extension '{extension}' maps to '{assetType.Value}'.");
        }
        return KeyForPath(normalized, assetType);
    }

    /// <summary>规范化路径 → 构建键（索引/类型校验 + 目录登记 + 导入设置指纹）；期望类型校验仅在依赖解析路径启用</summary>
    private AssetBuildKey KeyForPath(string normalized, AssetTypeId? expectedType)
    {
        if (!_index.TryGet(normalized, out var node) || node is null)
        {
            throw new InvalidOperationException(
                $"Asset path '{normalized}' is not present in the VFS index. "
                + "Complete the startup asset scan before loading assets.");
        }
        if (node.NodeType != VirtualNodeType.File)
            throw new InvalidOperationException($"Asset path '{normalized}' resolves to a directory, not a file.");
        var extension = Path.GetExtension(normalized);
        if (!_importers.TryGetAssetType(extension, out var assetType))
            throw new NotSupportedException($"No importer for extension '{extension}'");
        if (expectedType is { } expected && expected != assetType)
        {
            throw new InvalidDataException(
                $"Dependency path '{normalized}' expects asset type '{expected.Value}', "
                + $"but extension '{extension}' maps to '{assetType.Value}'.");
        }
        var record = _catalog.GetOrAdd(node.Id, assetType);
        var settings = new ImportSettings { Path = normalized };
        return AssetBuildKey.Create(
            record.AssetId, assetType, record.SourceRevision, importerRevision: 1, "", settings.ComputeFingerprint());
    }

    /// <summary>
    /// 载荷水合：材质载荷按解析结果替换占位句柄（Shader/MainTexture 取第一个对应类型的依赖）并携带
    /// 完整依赖句柄列表；其余载荷原样返回。
    /// </summary>
    private static IAssetPayload HydratePayload(
        AssetBuildKey key,
        IAssetPayload payload,
        IReadOnlyList<ResolvedDependency> resolved)
    {
        if (payload is not MaterialAsset material)
            return payload;

        var shader = default(AssetHandle<ShaderAsset>);
        AssetHandle<TextureAsset>? texture = null;
        var handles = new List<AssetHandle<IAssetPayload>>(resolved.Count);
        foreach (var dependency in resolved)
        {
            if (dependency.Type == AssetImporterRegistry.ShaderAssetTypeId)
                shader = new AssetHandle<ShaderAsset>(dependency.AssetId);
            else if (dependency.Type == AssetImporterRegistry.TextureAssetTypeId && texture is null)
                texture = new AssetHandle<TextureAsset>(dependency.AssetId);
            handles.Add(new AssetHandle<IAssetPayload>(dependency.AssetId));
        }
        return new MaterialAsset(
            material.Name, key.AssetId, shader, texture, material.Defaults, key.SourceRevision, handles);
    }

    /// <summary>FrameCommit 阶段（Main 域）：镜像结果依赖到内存反向索引，并单事务持久化依赖边。</summary>
    private void PersistDependencies(AssetPipelineResult result)
    {
        if (result.Dependencies.Count == 0)
            return;
        lock (_databaseGate)
        {
            DependencyIndex.ReplaceDependencies(result.Key.AssetId, ResolveDependencyIds(result.Dependencies));
            if (_database is null)
                return;
            var paths = new List<string>(result.Dependencies.Count);
            foreach (var dependency in result.Dependencies)
                paths.Add(_files.Normalize(dependency.LogicalPath));
            _database.WriteDependencyEdgesAsync(result.Key.AssetId, paths, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 依赖路径 → AssetId：优先用 AssetDB snapshot（Assets 按规范化路径对账，FileNodes/Assets 复用），
    /// 未接入数据库或快照滞后时回退运行期目录登记（与 Worker 解析结果恒等——磁盘模式 ID 为确定性生成）。
    /// </summary>
    private IReadOnlyList<AssetId> ResolveDependencyIds(IReadOnlyList<AssetImportDependency> dependencies)
    {
        Dictionary<string, AssetId>? pathToId = null;
        if (_database is not null)
        {
            var snapshot = _database.CaptureSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
            pathToId = snapshot.Assets.ToDictionary(
                asset => asset.LogicalPath, asset => asset.AssetId, StringComparer.Ordinal);
        }

        var ids = new List<AssetId>(dependencies.Count);
        foreach (var dependency in dependencies)
        {
            var normalized = _files.Normalize(dependency.LogicalPath);
            if (pathToId is not null && pathToId.TryGetValue(normalized, out var fromSnapshot))
            {
                ids.Add(fromSnapshot);
                continue;
            }
            if (_index.TryGet(normalized, out var node)
                && node is not null
                && _importers.TryGetAssetType(Path.GetExtension(normalized), out var assetType))
            {
                ids.Add(_catalog.GetOrAdd(node.Id, assetType).AssetId);
                continue;
            }
            throw new InvalidOperationException($"Dependency path '{normalized}' could not be mapped to an AssetId.");
        }
        return ids;
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

    /// <summary>依赖解析结果：声明 + 解析后的资产身份（Pipeline 内部）</summary>
    private sealed record ResolvedDependency(AssetImportDependency Declared, AssetId AssetId, AssetTypeId Type);

    private sealed class SharedJob(AssetBuildKey key)
    {
        public AssetBuildKey Key { get; } = key;

        public TaskCompletionSource<AssetPipelineResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Consumers;
    }
}