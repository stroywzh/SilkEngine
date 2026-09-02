using SilkEngine.Assets.Database;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
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
/// 构建产物缓存：接入 <see cref="BuildArtifactStore"/> 与序列化器时，Worker 构建前按键（含 fingerprint）查缓存——
/// 命中 → 经 <see cref="AssetSerializationService"/> 反序列化直接发布（跳过导入；结果不携带依赖列表，
/// 依赖边由最初导入的 FrameCommit 持久化，空列表使回写跳过，语义不变）；未命中 → 正常导入后序列化为
/// 派生字节写入缓存（写入失败仅告警不阻断构建）。任何缓存读取/解码/反序列化失败均回退导入路径。
/// 成功结果经 FrameCommit 阶段先持久化依赖边（Dependencies 表 + 内存 AssetDependencyIndex，单事务），
/// 再投递给 <see cref="ResultSink"/>（AssetManager 应用）。
/// 仅由 Host/AssetManager 内部使用（internal；业务经 AssetManager 门面）。
/// </summary>
internal sealed class AssetPipeline : IAssetPipeline, IAssetKeyResolver
{
    /// <summary>当前记录 schema 版本（内置序列化器统一 1；缓存写入与解析共用，演进时递增）</summary>
    private const int CurrentRecordSchemaVersion = 1;

    /// <summary>当前导入器修订号（内置导入器统一 1；导入器输出变化时递增）</summary>
    private const ulong DefaultImporterRevision = 1;

    private readonly IAssetFileSystem _files;
    private readonly IVirtualFileIndex _index;
    private readonly AssetCatalog _catalog;
    private readonly AssetImporterRegistry _importers;
    private readonly IBackgroundScheduler _background;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly ThreadRuntime _runtime;
    private readonly IAssetDatabase? _database;
    private readonly BuildArtifactStore? _artifactStore;
    private readonly AssetSerializerRegistry? _serializers;
    private readonly AssetSerializationService? _serialization;
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
    /// <param name="artifactStore">构建产物缓存存储（可为 null：禁用构建产物缓存）</param>
    /// <param name="serializers">序列化器注册表（可为 null：禁用序列化缓存往返）</param>
    public AssetPipeline(
        IAssetFileSystem files,
        IVirtualFileIndex index,
        AssetCatalog catalog,
        AssetImporterRegistry importers,
        IBackgroundScheduler background,
        IMainThreadDispatcher mainThread,
        ThreadRuntime runtime,
        IAssetDatabase? database = null,
        BuildArtifactStore? artifactStore = null,
        AssetSerializerRegistry? serializers = null)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _importers = importers ?? throw new ArgumentNullException(nameof(importers));
        _background = background ?? throw new ArgumentNullException(nameof(background));
        _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _database = database;
        _artifactStore = artifactStore;
        _serializers = serializers;
        if (artifactStore is not null && serializers is not null)
            _serialization = new AssetSerializationService(serializers, new ArtifactRecordResolver(this));
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
    /// 应用一次变更源结果（Main 域，EngineLoop 低频槽驱动，<see cref="AssetManager.ApplyAssetChanges"/> 转发）：
    /// 以重新扫描为准对账虚拟文件索引（内容指纹变化识别修改/新增/删除）→ 在 Main/FramCommit 事务中按变更
    /// 递增目录修订（<see cref="Invalidate"/>）、更新 AssetDB 文件节点（<see cref="AssetCatalog.ReconcileDatabase"/>）
    /// → 沿 <see cref="AssetDependencyIndex.InvalidateCascade"/> 级联失效受影响的依赖方（任务 5 遗留接线点）。
    /// 不启动任何缓存重建：重建决策归 <see cref="AssetManager"/>（按缓存条目存在性调用 <see cref="StartRebuild"/>）。
    /// 变更事件可重复/合并，本方法幂等（重复对账无新增量即返回空）。
    /// </summary>
    /// <param name="changes">变更源快照（仅作触发信号；权威变更集以重扫得到的索引增量为准）</param>
    /// <returns>受影响资产（含级联）与源已删除的资产集合</returns>
    internal AssetChangeApplyResult ApplyAssetChanges(ChangeSourceResult changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var apply = _index.Apply(_files.Scan());
        if (apply.Changes.Count == 0)
            return AssetChangeApplyResult.Empty;

        var affected = new List<AssetId>();
        var removed = new List<AssetId>();
        foreach (var change in apply.Changes)
        {
            switch (change.Kind)
            {
                case VirtualChangeKind.Added:
                case VirtualChangeKind.Modified:
                    foreach (var record in _catalog.GetForSourceNode(change.NodeId))
                    {
                        Invalidate(record.AssetId); // 目录修订递增 + 清理已完成缓存作业
                        if (!affected.Contains(record.AssetId))
                            affected.Add(record.AssetId);
                        _catalog.ReconcileDatabase(record); // 单事务更新 FileNodes/Assets（新指纹与新修订）
                    }
                    break;
                case VirtualChangeKind.Removed:
                    foreach (var record in _catalog.GetForSourceNode(change.NodeId))
                    {
                        if (!affected.Contains(record.AssetId))
                        {
                            affected.Add(record.AssetId);
                            removed.Add(record.AssetId);
                        }
                    }
                    break;
            }
        }

        // 级联失效：被失效依赖的依赖方（传递闭包）目录修订同步递增，等待 AssetManager 按其缓存条目重建
        for (var i = 0; i < affected.Count; i++)
        {
            var seed = affected[i];
            foreach (var dependent in DependencyIndex.InvalidateCascade(seed))
            {
                if (affected.Contains(dependent))
                    continue;
                affected.Add(dependent);
                Invalidate(dependent);
            }
        }

        return new AssetChangeApplyResult(affected, removed);
    }

    /// <summary>
    /// 立即按目录当前修订重建指定资产（Hot Reload 消费端；作业无外部消费者）。
    /// 成功/失败结果均经 FrameCommit 投递；与普通<see cref="Request{T}"/> 同键去重合并。
    /// </summary>
    /// <param name="assetId">资产标识（目录记录必须存在）</param>
    internal void StartRebuild(AssetId assetId)
    {
        if (!_catalog.TryGet(assetId, out var record) || record is null)
            return;
        if (!_index.TryGet(record.SourceNodeId, out var node) || node is null)
            return;
        var settings = new ImportSettings { Path = node.LogicalPath };
        var key = AssetBuildKey.Create(
            record.AssetId, record.AssetTypeId, record.SourceRevision,
            DefaultImporterRevision, "", settings.ComputeFingerprint());
        lock (_inflight)
        {
            if (_inflight.ContainsKey(key))
                return;
            var job = new SharedJob(key) { Consumers = 0 };
            _inflight[key] = job;
            Interlocked.Increment(ref _executionCount);
            StartWorker(job, [key]);
        }
    }

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
            {
                if (_inflight.Remove(key, out var job))
                    job.Cancel.Dispose(); // 已完成作业不再被取消路径触碰（DetachConsumer 门控）→ 安全释放
            }
        }
    }

    /// <summary>按资产查询其源逻辑路径（目录/索引未命中返回 null；错误诊断消息用）。</summary>
    /// <param name="assetId">资产标识</param>
    /// <returns>源逻辑路径；未命中为 null</returns>
    public string? TryGetLogicalPath(AssetId assetId)
    {
        if (_catalog.TryGet(assetId, out var record)
            && record is not null
            && _index.TryGet(record.SourceNodeId, out var node)
            && node is not null)
        {
            return node.LogicalPath;
        }
        return null;
    }

    /// <summary>
    /// 关闭：取消全部在途作业并清空 in-flight 表（AssetManager.Dispose 调用）。
    /// 在途结果因取消不再投递 FrameCommit（过期 ResultBatch 丢弃）；持锁保证与取消/驱逐无竞态。
    /// </summary>
    internal void CancelPendingJobs()
    {
        lock (_inflight)
        {
            foreach (var job in _inflight.Values)
            {
                job.Cancel.Cancel();
                job.Completion.TrySetCanceled();
                job.Cancel.Dispose();
            }
            _inflight.Clear();
        }
    }

    /// <inheritdoc />
    public AssetOperation<T> Request<T>(AssetBuildKey key, CancellationToken cancellationToken = default)
        where T : class, IAssetPayload
    {
        var gate = new ConsumerGate<T>(this, GetOrStartJob(key, null));
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(gate.Cancel);
        return new AssetOperation<T>(key.AssetId, gate.Task, gate.Cancel, _mainThread, _runtime);
    }

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
            var job = new SharedJob(key)
            {
                Consumers = 1, // 新作业由首个消费者持有（共享构建跨消费者合并的计数基准）
            };
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
        // 调用方持有 _inflight 锁：全部取消/关闭路径（DetachConsumer/Invalidate/CancelPendingJobs）也须持锁，
        // 故此处创建取消链接源时 job.Cancel 不可能已被 Dispose；Worker 捕获 token 后即使源被释放仍可取消。
        var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Cancel.Token);
        var cancellation = linked.Token;
        _background.Run(async ct =>
        {
            using var _ = linked;
            try
            {
                var result = await BuildResultAsync(job, chain, cancellation).ConfigureAwait(false);
                // 结果只在仍被接受的条件下回写；已取消（全部分调用方取消或关闭）时连同 FrameCommit 整体丢弃，
                // 不投递过期 ResultBatch（不复活已驱逐条目、不触发未提交 GPU 创建）
                if (!job.Completion.TrySetResult(result))
                    return;
                PostFrameCommit(result);
            }
            catch (OperationCanceledException)
            {
                job.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                job.Completion.TrySetException(ex);
                // 失败结果同样经 FrameCommit 投递：无人消费的重载/变更检测构建依赖此通道暴露失败
                // （消费方按状态与源修订决定落账/保留上一版载荷），普通加载方仍经作业异常观察失败
                PostFrameCommit(new AssetPipelineResult(
                    job.Key, null, [], AssetPipelineResultState.Failed, ex));
            }
        });
    }

    /// <summary>FrameCommit 阶段（Main 域）：先持久化成功结果的依赖边，再把结果投递给 <see cref="ResultSink"/>。</summary>
    private void PostFrameCommit(AssetPipelineResult result)
    {
        _mainThread.Post(MainThreadPhase.FrameCommit, () =>
        {
            PersistDependencies(result);
            ResultSink?.Invoke(result);
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

        // 构建产物缓存：命中直接发布（跳过导入）；任何失效/损坏/反序列化失败均回退导入
        if (_artifactStore is not null && _serialization is not null)
        {
            var fromCache = await TryBuildFromArtifactAsync(key, ct).ConfigureAwait(false);
            if (fromCache is not null)
                return fromCache;
        }

        var source = await _files.ReadAsync(path).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var import = importer.Import(source, new AssetImportContext(path, settings));

        var resolved = await ResolveDependenciesAsync(import.Dependencies, chain, ct).ConfigureAwait(false);
        ValidateFreshness(key, import.ImporterRevision);
        var payload = HydratePayload(key, import.Payload, resolved);
        if (_artifactStore is not null && _serialization is not null)
            await CacheArtifactAsync(key, payload, import.ImporterRevision, ct).ConfigureAwait(false);
        return new AssetPipelineResult(
            key,
            payload,
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
            record.AssetId, assetType, record.SourceRevision, DefaultImporterRevision, "", settings.ComputeFingerprint());
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

    /// <summary>
    /// 构建产物缓存命中路径：按 BuildKey（含 fingerprint）查存储 → 解码记录 → 校验记录语义三元组
    /// （BuildKey/SourceFingerprint/ImporterRevision 必须同时一致）→ 校验修订新鲜度 → 经
    /// <see cref="AssetSerializationService"/> 反序列化直接发布。
    /// 缓存命中结果不携带依赖列表：记录只含载荷声明的句柄依赖（如材质缺网格路径依赖），
    /// 依赖边已由最初导入构建的 FrameCommit 持久化，空列表使 <see cref="PersistDependencies"/> 跳过回写，
    /// 维持任务 5 的依赖解析与回写语义不变。
    /// 失败语义：解码损坏/三元组失配/依赖记录缺失或不支持 → 视为 miss 返回 null，由调用方回退导入；
    /// 修订过期（<see cref="AssetStaleResultException"/>）与取消（<see cref="OperationCanceledException"/>）照常抛出。
    /// </summary>
    /// <returns>缓存命中结果；任何失配/损坏回退时为 null</returns>
    private async Task<AssetPipelineResult?> TryBuildFromArtifactAsync(AssetBuildKey key, CancellationToken ct)
    {
        try
        {
            var keyString = KeyString(key);
            var bytes = await _artifactStore!.TryLoadAsync(keyString, ct).ConfigureAwait(false);
            if (bytes is null)
                return null;

            var record = _serialization!.DecodeRecord(bytes.Value);
            if (!string.Equals(record.BuildKey, keyString, StringComparison.Ordinal)
                || !string.Equals(record.SourceFingerprint, SourceFingerprint(key.AssetId), StringComparison.Ordinal)
                || record.ImporterRevision != key.ImporterRevision)
            {
                return null; // 记录语义三元组与当前键不符 → 视为 miss
            }

            ValidateFreshness(key, DefaultImporterRevision);
            var data = _serialization.Deserialize(record).Asset as IAssetPayload
                ?? throw new InvalidDataException($"构建产物反序列化结果不是 IAssetPayload（{keyString}）");
            ct.ThrowIfCancellationRequested();
            return new AssetPipelineResult(key, data, [], AssetPipelineResultState.Succeeded, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or NotSupportedException)
        {
            if (LogConfig.Assets)
                Log.Warning($"[AssetPipeline] 构建产物缓存失效，回退导入：{KeyString(key)}：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 构建产物缓存写入：按导入结果序列化为记录（附 BuildKey/SourceFingerprint/ImporterRevision 语义
    /// 与源节点）后经 <see cref="AssetSerializationService"/> 编码为派生字节存入缓存。
    /// 缓存为加速手段：任何写入失败仅告警不阻断构建；取消照常传播（不落盘，临时文件由存储清理）。
    /// </summary>
    /// <param name="key">构建键</param>
    /// <param name="payload">已水合的最终载荷（缓存反序列化的还原对象，不含实例覆盖）</param>
    /// <param name="importerRevision">本次导入的导入器修订号</param>
    /// <param name="ct">取消令牌</param>
    private async Task CacheArtifactAsync(AssetBuildKey key, IAssetPayload payload, ulong importerRevision, CancellationToken ct)
    {
        try
        {
            var serializer = _serializers!.Resolve(key.AssetType, CurrentRecordSchemaVersion);
            var keyString = KeyString(key);
            var record = serializer.Serialize(payload) with
            {
                BuildKey = keyString,
                SourceFingerprint = SourceFingerprint(key.AssetId),
                ImporterRevision = importerRevision,
                SourceNodeId = CatalogSourceNode(key.AssetId),
            };
            await _artifactStore!.SaveAsync(keyString, _serialization!.EncodeRecord(record), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (LogConfig.Assets)
                Log.Warning($"[AssetPipeline] 构建产物缓存写入失败（忽略）：{KeyString(key)}：{ex.Message}");
        }
    }

    /// <summary>查询资产当前源内容指纹（目录/索引未命中或指纹缺失时为空串；与目录对账语义一致）</summary>
    private string SourceFingerprint(AssetId assetId)
    {
        if (_catalog.TryGet(assetId, out var record)
            && record is not null
            && _index.TryGet(record.SourceNodeId, out var node)
            && node is not null)
        {
            return node.MetaData?.SourceFingerprint ?? string.Empty;
        }
        return string.Empty;
    }

    /// <summary>查询资产的源虚拟节点（目录未命中返回 null）</summary>
    private VirtualNodeId? CatalogSourceNode(AssetId assetId)
        => _catalog.TryGet(assetId, out var record) && record is not null ? record.SourceNodeId : null;

    /// <summary>构建键 → 缓存稳定键字符串（确定性；含指纹，路径安全字符）</summary>
    private static string KeyString(AssetBuildKey key) =>
        $"{key.AssetId.Value:N}|{key.AssetType.Value}|{key.SourceRevision}|{key.ImporterRevision}|{key.TargetProfile}|{key.ImportSettingsFingerprint}";

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
            // 最后一个消费者离开且作业未完成：取消 Worker（连同临时缓存写入的 ct）并整体移除
            if (job.Consumers <= 0 && !job.Completion.Task.IsCompleted)
            {
                job.Cancel.Cancel();
                job.Cancel.Dispose(); // worker 已捕获 token（StartWorker 持锁创建），释放源不影响其取消
                job.Completion.TrySetCanceled();
                _inflight.Remove(job.Key);
            }
        }
    }

    /// <summary>
    /// 构建产物记录解析器（缓存命中路径的记录目录）：按 AssetId 从目录/索引重算依赖构建键
    /// （源修订 + 导入设置指纹），经 <see cref="BuildArtifactStore"/> 加载并解码记录，
    /// 校验语义三元组一致后才返回（不一致即 null，服务层据此判定依赖缺失回退导入）。
    /// 反序列化器只消费句柄，<see cref="Resolve"/> 不解析对象。同步等待安全：本地磁盘读取无真正异步 IO。
    /// </summary>
    private sealed class ArtifactRecordResolver(AssetPipeline pipeline) : IAssetReferenceResolver
    {
        /// <summary>按 ID 从构建产物缓存读取记录；目录/索引/缓存未命中或语义不符返回 null</summary>
        public AssetSerializationRecord? TryGetRecord(AssetId assetId)
        {
            if (!pipeline._catalog.TryGet(assetId, out var record) || record is null)
                return null;
            if (!pipeline._index.TryGet(record.SourceNodeId, out var node) || node is null)
                return null;

            var fingerprint = node.MetaData?.SourceFingerprint ?? string.Empty;
            var settings = new ImportSettings { Path = node.LogicalPath };
            var key = AssetBuildKey.Create(
                record.AssetId, record.AssetTypeId, record.SourceRevision,
                DefaultImporterRevision, "", settings.ComputeFingerprint());
            var keyString = KeyString(key);
            var bytes = pipeline._artifactStore!.TryLoadAsync(keyString, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (bytes is null)
                return null;

            try
            {
                var decoded = pipeline._serialization!.DecodeRecord(bytes.Value);
                if (!string.Equals(decoded.BuildKey, keyString, StringComparison.Ordinal)
                    || !string.Equals(decoded.SourceFingerprint, fingerprint, StringComparison.Ordinal)
                    || decoded.ImporterRevision != DefaultImporterRevision)
                {
                    return null;
                }
                return decoded;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>强类型句柄解析（反序列化器不依赖对象解析；返回 null）</summary>
        public T Resolve<T>(AssetHandle<T> handle)
            where T : class => null!;

        /// <summary>非泛型句柄解析（反序列化器不依赖对象解析；返回 null）</summary>
        public object Resolve(UntypedAssetHandle handle) => null!;
    }

    /// <summary>依赖解析结果：声明 + 解析后的资产身份（Pipeline 内部）</summary>
    private sealed record ResolvedDependency(AssetImportDependency Declared, AssetId AssetId, AssetTypeId Type);

    /// <summary>变更对账结果：受影响资产（含级联依赖方）与源已删除的资产（Pipeline 内部）</summary>
    /// <param name="AffectedAssets">需要失效/重建的资产（含级联依赖方；不含未受影响资产）</param>
    /// <param name="RemovedAssets">源文件已删除的资产（由 AssetManager 标记 Missing）</param>
    internal sealed record AssetChangeApplyResult(
        IReadOnlyList<AssetId> AffectedAssets,
        IReadOnlyList<AssetId> RemovedAssets)
    {
        /// <summary>无变更的空结果（单例）</summary>
        public static AssetChangeApplyResult Empty { get; } = new([], []);
    }

    private sealed class SharedJob(AssetBuildKey key)
    {
        public AssetBuildKey Key { get; } = key;

        public TaskCompletionSource<AssetPipelineResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>作业级取消源：全部消费者离开或关闭时取消（Worker 经链接 token 观察；持 _inflight 锁创建/取消/释放）</summary>
        public CancellationTokenSource Cancel { get; } = new();

        public int Consumers;
    }

    /// <summary>
    /// 单消费者门：把共享作业完成发布到本消费者独立任务；<see cref="Cancel"/> 只完成当前操作，
    /// 并回调管线把共享作业的消费者计数递减（最后一个消费者取消时才取消 Worker）。
    /// </summary>
    private sealed class ConsumerGate<T>
        where T : class, IAssetPayload
    {
        private readonly AssetPipeline _pipeline;
        private readonly SharedJob _job;
        private readonly TaskCompletionSource<T> _completion;
        private int _cancelRequested;

        public ConsumerGate(AssetPipeline pipeline, SharedJob job)
        {
            _pipeline = pipeline;
            _job = job;
            _completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            job.Completion.Task.ContinueWith(
                static (completed, state) => ((ConsumerGate<T>)state!).Propagate(completed),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>本消费者视角的完成任务（取消/异常/结果均落在其上）</summary>
        public Task<T> Task => _completion.Task;

        /// <summary>取消当前操作：释放共享作业上的消费者 + 完成本操作（幂等）</summary>
        public void Cancel()
        {
            if (Interlocked.Exchange(ref _cancelRequested, 1) != 0)
                return;
            _pipeline.DetachConsumer(_job);
            _completion.TrySetCanceled();
        }

        private void Propagate(Task<AssetPipelineResult> completed)
        {
            if (completed.IsFaulted)
                _completion.TrySetException(completed.Exception!.GetBaseException());
            else if (completed.IsCanceled)
                _completion.TrySetCanceled();
            else
                _completion.TrySetResult((T)completed.Result.Payload!);
        }
    }
}
