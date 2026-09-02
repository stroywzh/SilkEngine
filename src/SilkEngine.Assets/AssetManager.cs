using System.Collections.Concurrent;
using SilkEngine.Assets.Binding;
using SilkEngine.Assets.Database;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Threading;

namespace SilkEngine.Assets;

/// <summary>
/// 资产门面（Main 域专用 API）：Payload 缓存、状态提交与驻留。
/// 构造注入 <see cref="IAssetPipeline"/>（路径解析与执行）、主线程派发器与线程运行时；
/// 构造即自注册进 Services。
/// 本类不持有 Importer、不创建线程、不调度 Worker；结果经 Pipeline 的 FrameCommit 投递由
/// <see cref="ApplyPipelineResult"/> 应用到缓存。
/// </summary>
public sealed class AssetManager : IDisposable
{
    private readonly IAssetPipeline _pipeline;
    private readonly IAssetKeyResolver _keyResolver;
    private readonly IMainThreadDispatcher _mainThread;
    private readonly ThreadRuntime _runtime;
    private readonly AssetSerializerRegistry _serializerRegistry;
    private readonly AssetCache _cache = new();
    private readonly ConcurrentQueue<RenderResourceReleaseRequest> _renderReleases = new();
    private readonly Dictionary<AssetId, int> _residency = new();
    private readonly AssetGpuResourceCache _gpuCache = new();
    private readonly List<RenderResourceCreateItem> _pendingCreates = [];
    private readonly AssetRenderBridge _bridge;
    private IAssetDatabase? _database;
    private ulong _nextRequestId;

    /// <summary>最近一次 GPU 句柄发布时的线程域（测试断言用；未发布为 Unknown）。</summary>
    internal ThreadDomain LastPublishDomainForTests { get; private set; } = ThreadDomain.Unknown;

    /// <summary>最近一次 GPU 创建/编译失败的阶段（测试断言用；未失败为 null）。</summary>
    internal string? LastFailureStageForTests { get; private set; }

    /// <summary>最近一次 GPU 创建/编译失败的详情消息（测试断言用；未失败为 null）。</summary>
    internal string? LastFailureMessageForTests { get; private set; }

    /// <summary>
    /// 受控引用解析器视图：按 AssetId 从本管理器缓存解析已加载载荷（序列化层唯一资产访问边界，无全局服务定位）。
    /// 本管理器不持有序列化记录，<see cref="IAssetReferenceResolver.TryGetRecord"/> 恒返回 null。
    /// </summary>
    public IAssetReferenceResolver Resolver { get; }

    /// <summary>
    /// 构造注入管线、主线程派发器与线程运行时（管线须支持路径解析与 FrameCommit 结果投递）。
    /// 构造不注册全局服务（Host 兼容阶段集中注册）。
    /// </summary>
    /// <param name="pipeline">资产管线（路径解析 + 构建执行）</param>
    /// <param name="mainThread">主线程派发器（结果应用阶段）</param>
    /// <param name="runtime">线程运行时（域判定与关闭令牌）</param>
    /// <param name="serializerRegistry">序列化器注册表；null 时新建空注册表实例（实例级互不共享）</param>
    public AssetManager(
        IAssetPipeline pipeline,
        IMainThreadDispatcher mainThread,
        ThreadRuntime runtime,
        AssetSerializerRegistry? serializerRegistry = null
    )
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _keyResolver =
            pipeline as IAssetKeyResolver
            ?? throw new InvalidOperationException("管线必须支持路径解析与结果投递。");
        _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _serializerRegistry = serializerRegistry ?? new AssetSerializerRegistry();
        _keyResolver.ResultSink = ApplyPipelineResult;
        Resolver = new CatalogReferenceResolver(this);
        _bridge = new AssetRenderBridge(new ReleaseOnlySink(this));
    }

    /// <summary>
    /// 创建磁盘目录驱动的资产管理器（Host 组合根入口）：构造资产管线（VFS 索引 + 启动扫描 +
    /// 导入器注册表 + SQLite AssetDB）并注入线程运行时；管线保持内部类型，公开面不外泄。
    /// AssetDB 位于 libraryRoot 目录（默认 assetRoot/Library），持久化依赖边与构建元数据；
    /// 构建产物缓存（<see cref="BuildArtifactStore"/>）位于 libraryRoot/SilkEngine/AssetDB/cache，
    /// 缓存保存序列化派生字节，JSON `.asset` 等源文件始终由导入器在资产根目录读取，缓存不保存源副本。
    /// </summary>
    /// <param name="assetRoot">资产根目录（null 时缺省 "Assets"）</param>
    /// <param name="runtime">线程运行时（Worker 池与主线程派发器来源）</param>
    /// <param name="files">文件服务（null 时按 assetRoot 构造磁盘文件服务；测试注入读取门控等包装用）</param>
    /// <param name="projectNamespace">项目命名空间（磁盘资产身份输入之一；null 时缺省 "sandbox"）</param>
    /// <param name="libraryRoot">资产库目录（AssetDB 存储位置；null 时缺省 assetRoot/Library）</param>
    /// <returns>已完成启动扫描的资产管理器</returns>
    public static AssetManager CreateDiskBacked(
        string? assetRoot,
        ThreadRuntime runtime,
        IAssetFileSystem? files = null,
        string? projectNamespace = null,
        string? libraryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        projectNamespace ??= "sandbox";
        var fileService = files ?? new DiskAssetFileSystem(assetRoot ?? "Assets");
        var index = new InMemoryVirtualFileIndex();
        var libraryPath = libraryRoot ?? Path.Combine(assetRoot ?? "Assets", "Library");
        var databasePath = Path.Combine(libraryPath, "assetdb.sqlite");
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(databaseDirectory))
            Directory.CreateDirectory(databaseDirectory);
        var database = new SqliteAssetDatabase(databasePath);
        database.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        var serializers = new AssetSerializerRegistry();
        RegisterBuiltInSerializers(serializers);
        var artifacts = new BuildArtifactStore(Path.Combine(libraryPath, "SilkEngine", "AssetDB", "cache"));
        var pipeline = new AssetPipeline(
            fileService,
            index,
            new AssetCatalog(projectNamespace, index, database),
            new AssetImporterRegistry(),
            runtime.Background,
            runtime.MainThread,
            runtime,
            database,
            artifacts,
            serializers
        );
        pipeline.ApplyScan(fileService.Scan());
        var manager = new AssetManager(
            pipeline,
            runtime.MainThread,
            runtime,
            serializers
        );
        manager.AttachDatabase(database);
        return manager;
    }

    /// <summary>注册内置资产序列化器（纹理/着色器/网格/材质；构建产物缓存与记录往返依赖）</summary>
    /// <param name="registry">目标注册表</param>
    private static void RegisterBuiltInSerializers(AssetSerializerRegistry registry)
    {
        registry.Register(new TextureAssetSerializer());
        registry.Register(new ShaderAssetSerializer());
        registry.Register(new MeshAssetSerializer());
        registry.Register(new MaterialAssetSerializer());
    }

    /// <summary>注册序列化器（直通注册表；同类型重复注册抛 <see cref="InvalidOperationException"/>）</summary>
    /// <param name="serializer">待注册序列化器</param>
    public void RegisterSerializer(IAssetSerializer serializer) =>
        _serializerRegistry.Register(serializer);

    /// <summary>按类型与 schema 版本解析序列化器（直通注册表；未知类型或版本不支持抛 <see cref="NotSupportedException"/>）</summary>
    /// <param name="typeId">资产类型标识</param>
    /// <param name="schemaVersion">记录 schema 版本</param>
    /// <returns>匹配的序列化器</returns>
    public IAssetSerializer ResolveSerializer(AssetTypeId typeId, int schemaVersion) =>
        _serializerRegistry.Resolve(typeId, schemaVersion);

    /// <summary>释放：注销服务定位器中的自注册并关闭 AssetDB（幂等；框架生命周期仍由 Services.Shutdown 反序管理）</summary>
    public void Dispose()
    {
        Services.Unregister<AssetManager>();
        if (_database is not null)
        {
            _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _database = null;
        }
    }

    /// <summary>挂接磁盘管线自建的资产数据库（CreateDiskBacked 调用；Dispose 时关闭）</summary>
    /// <param name="database">资产数据库（可为 null）</param>
    internal void AttachDatabase(IAssetDatabase? database) => _database = database;

    /// <summary>测试断言用：底层管线实例（磁盘管线为 AssetPipeline；其他实现为 null）</summary>
    internal AssetPipeline? PipelineForTests => _keyResolver as AssetPipeline;

    /// <summary>
    /// 将外部任务包装为业务安全操作：不改变外部 Task 执行域，只把完成发布纳入 Main 安全阶段；
    /// 取消只影响本操作。经 <see cref="AssetOperation{T}.FromTask"/> 调用。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="task">外部任务</param>
    /// <returns>安全操作</returns>
    internal AssetOperation<T> WrapExternalTask<T>(Task<T> task)
        where T : class, IAssetPayload => new(default, task, null, _mainThread, _runtime);

    /// <summary>
    /// 完全同步加载（仅建议启动初始化与小资产）：解析已索引路径 → 获取 BuildKey → 同步等待 Pipeline。
    /// 失败直接抛出（含 <see cref="AssetStaleResultException"/> 与导入异常）。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <returns>规范 Payload 实例</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录（详细消息）</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public T Load<T>(string path)
        where T : class, IAssetPayload
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var key = _keyResolver.ResolveKey(path);
        return _pipeline.Request<T>(key).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步加载：解析已索引路径 → 获取 BuildKey → 创建/附加 Pipeline 安全操作。
    /// 同键请求合并（读取与导入只执行一次）；返回规范 Payload 实例。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <param name="cancellationToken">取消令牌（只取消当前调用方视角）</param>
    /// <returns>安全资产操作（默认 await 在 Main 安全阶段恢复）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录（详细消息）</exception>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public AssetOperation<T> LoadAsync<T>(
        string path,
        CancellationToken cancellationToken = default
    )
        where T : class, IAssetPayload
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var key = _keyResolver.ResolveKey(path);
        return _pipeline.Request<T>(key, cancellationToken);
    }

    /// <summary>
    /// 解析已索引路径为稳定资产句柄（不触发加载；Payload 经 <see cref="TryResolve{T}(AssetHandle{T}, out T?)"/> 解析）。
    /// 用于渲染器资产属性等需要以句柄绑定索引资产的场景；句柄铸造收敛于本方法，调用方不自造随机 ID。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <returns>资产句柄</returns>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录（详细消息）</exception>
    public AssetHandle<T> GetHandle<T>(string path)
        where T : class, IAssetPayload
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var key = _keyResolver.ResolveKey(path);
        return new AssetHandle<T>(key.AssetId);
    }

    /// <summary>源变更失效：目录记录修订号递增并移除已完成缓存作业（下次访问重新构建；在途作业完成后按过期校验失败）。</summary>
    /// <param name="path">资产逻辑路径（相对文件服务根目录）</param>
    /// <exception cref="ArgumentException">path 为 null/空白或非法路径</exception>
    /// <exception cref="InvalidOperationException">路径未进入 VFS 索引或解析到目录</exception>
    public void Invalidate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _keyResolver.Invalidate(_keyResolver.ResolveKey(path).AssetId);
    }

    /// <summary>创建并绑定资产驻留槽（Slot 持有期间 Payload 不被 <see cref="UnloadUnused"/> 驱逐）</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="handle">资产句柄</param>
    /// <returns>已绑定的驻留槽</returns>
    public AssetSlot<T> CreateSlot<T>(AssetHandle<T> handle)
        where T : class, IAssetPayload
    {
        var slot = new AssetSlot<T>(this);
        slot.Bind(handle);
        return slot;
    }

    /// <summary>创建并绑定资产租赁（Pin/Lease 持有期间 Payload 不被 <see cref="UnloadUnused"/> 驱逐）</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="handle">资产句柄</param>
    /// <returns>已绑定的资产租赁</returns>
    public AssetLease<T> Pin<T>(AssetHandle<T> handle)
        where T : class, IAssetPayload
    {
        var lease = new AssetLease<T>(this);
        lease.Bind(handle);
        return lease;
    }

    /// <summary>登记驻留持有（Slot/Lease 经 Bind 调用）</summary>
    /// <param name="assetId">资产标识</param>
    internal void AddResidency(AssetId assetId) =>
        _residency[assetId] = _residency.GetValueOrDefault(assetId) + 1;

    /// <summary>释放驻留持有（幂等；下限 0）</summary>
    /// <param name="assetId">资产标识</param>
    internal void ReleaseResidency(AssetId assetId)
    {
        if (_residency.TryGetValue(assetId, out var count) && count > 0)
        {
            if (count == 1)
                _residency.Remove(assetId);
            else
                _residency[assetId] = count - 1;
        }
    }

    /// <summary>
    /// 帧末驱逐（Main 域）：移除无 Slot/Lease/Pin 持有的 Ready Payload；
    /// 已发布 GPU 句柄的条目经 <see cref="RenderResourceReleaseRequest"/> 入队（渲染线程帧首消费）。
    /// </summary>
    public void UnloadUnused()
    {
        foreach (var entry in _cache.All().ToArray())
        {
            if (entry.State != AssetState.Ready || entry.Payload is null)
                continue;
            if (_residency.GetValueOrDefault(entry.AssetId) > 0)
                continue;
            entry.State = AssetState.Unloaded;
            _cache.SetPayload(entry, null);
            foreach (var release in _gpuCache.EvictAll(entry.AssetId, entry.SourceRevision))
                _renderReleases.Enqueue(release);
        }
    }

    /// <summary>登记纹理 GPU 句柄（渲染侧创建完成后回填；驱逐时经缓存生成释放请求）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="handle">渲染侧句柄</param>
    internal void PublishRenderTexture(AssetId assetId, RenderTextureHandle handle)
    {
        var revision = _cache.Find(assetId)?.SourceRevision ?? 0UL;
        _gpuCache.Publish(assetId, revision, handle);
    }

    /// <summary>登记网格 GPU 句柄（渲染侧创建完成后回填；驱逐时经缓存生成释放请求）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="handle">渲染侧句柄</param>
    internal void PublishRenderMesh(AssetId assetId, RenderMeshHandle handle)
    {
        var revision = _cache.Find(assetId)?.SourceRevision ?? 0UL;
        _gpuCache.Publish(assetId, revision, handle);
    }

    /// <summary>登记着色器 GPU 句柄（渲染侧创建完成后回填；驱逐时经缓存生成释放请求）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="handle">渲染侧句柄</param>
    internal void PublishRenderShader(AssetId assetId, RenderShaderHandle handle)
    {
        var revision = _cache.Find(assetId)?.SourceRevision ?? 0UL;
        _gpuCache.Publish(assetId, revision, handle);
    }

    /// <summary>按资产查询已登记 GPU 句柄（渲染器句柄解析的业务入口；未登记返回 false）。</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="kind">资源种类</param>
    /// <param name="handle">已登记句柄（未登记为 0）</param>
    /// <returns>查询命中为 true</returns>
    public bool TryGetRenderHandle(AssetId assetId, RenderResourceKind kind, out ulong handle)
    {
        var revision = _cache.Find(assetId)?.SourceRevision ?? 0UL;
        return _gpuCache.TryGet(assetId, revision, kind, out handle);
    }

    /// <summary>
    /// 注册瞬态资产（不经 VFS/目录）：Main 域专用，Payload 立即就绪并排队 GPU 创建请求。
    /// ID 由引擎资产层生成（<see cref="AssetId"/> 包装新 GUID），调用方不能自造句柄。
    /// </summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="payload">规范载荷实例</param>
    /// <returns>稳定资产句柄</returns>
    /// <exception cref="ArgumentNullException">payload 为 null</exception>
    public AssetHandle<T> RegisterTransient<T>(T payload)
        where T : class, IAssetPayload
    {
        ArgumentNullException.ThrowIfNull(payload);
        ((IThreadGuard)_runtime).Assert(ThreadDomain.Main, "AssetManager.RegisterTransient");
        var id = new AssetId(Guid.NewGuid());
        var entry = _cache.GetOrAdd(id);
        entry.State = AssetState.Ready;
        entry.SourceRevision = 0UL;
        _cache.SetPayload(entry, payload);
        QueueGpuCreation(id, entry.SourceRevision, payload);
        return new AssetHandle<T>(id);
    }

    /// <summary>将待提交创建请求批量提升为创建批次并清空（Main 域；提交前由宿主调用）。</summary>
    /// <returns>创建批次（无待创建请求时为空批次）</returns>
    public RenderResourceCreateBatch DrainCreateBatch()
    {
        if (_pendingCreates.Count == 0)
            return RenderResourceCreateBatch.Empty;
        var batch = new RenderResourceCreateBatch(_pendingCreates.ToArray());
        _pendingCreates.Clear();
        return batch;
    }

    /// <summary>刷新待创建请求（当前阶段注册时同步入队；为未来批量/节流刷新保留扩展点）。</summary>
    internal void FlushPendingRenderCreates() { }

    /// <summary>
    /// 应用渲染线程回传的创建结果批次（Main 域，SubmitFrame 返回后调用）：
    /// 按 RequestId 关联资产身份并校验当前修订；匹配时发布句柄，过期时只生成句柄释放请求。
    /// </summary>
    /// <param name="results">创建结果批次</param>
    public void ApplyCreateResults(RenderResourceCreateResultBatch results)
    {
        ((IThreadGuard)_runtime).Assert(ThreadDomain.Main, "AssetManager.ApplyCreateResults");
        foreach (var result in results.Results)
        {
            if (!_gpuCache.TryResolveRequest(result.RequestId, out var tracked) || tracked is null)
                continue; // 未知请求（已应用或已取消）：跳过
            _gpuCache.RemoveRequest(result.RequestId);
            var entry = _cache.Find(tracked.AssetId);
            if (entry is null || entry.SourceRevision != tracked.Revision)
            {
                // 过期结果：不发布，仅生成句柄释放请求（渲染线程帧首消费）
                if (
                    result.State == RenderResourceCreateResultState.Succeeded
                    && result.Handle.Value != 0
                )
                    _renderReleases.Enqueue(
                        new RenderResourceReleaseRequest(tracked.Kind, result.Handle.Value)
                    );
                continue;
            }
            if (result.State != RenderResourceCreateResultState.Succeeded)
            {
                // 编译/加载失败：阶段信息（Render 域携带）与错误消息按 RequestId 落账，不发布句柄
                var stage = result.Stage ?? tracked.Kind.ToString();
                var message = result.Error?.Message ?? "未知创建失败（无错误消息）";
                _gpuCache.RecordFailure(result.RequestId, stage, message);
                LastFailureStageForTests = stage;
                LastFailureMessageForTests = message;
                Log.Error($"[AssetManager] GPU 创建失败 ({tracked.Kind}) [stage={stage}]: {message}");
                continue;
            }
            PublishRenderHandle(tracked.AssetId, tracked.Kind, result.Handle.Value);
            LastPublishDomainForTests = _runtime.CurrentDomain;
        }
    }

    /// <summary>按种类发布 GPU 句柄（结果校验通过后回填缓存）。</summary>
    private void PublishRenderHandle(AssetId assetId, RenderResourceKind kind, ulong handle)
    {
        switch (kind)
        {
            case RenderResourceKind.Texture:
                _gpuCache.Publish(
                    assetId,
                    _cache.Find(assetId)?.SourceRevision ?? 0UL,
                    new RenderTextureHandle(handle)
                );
                break;
            case RenderResourceKind.Shader:
                _gpuCache.Publish(
                    assetId,
                    _cache.Find(assetId)?.SourceRevision ?? 0UL,
                    new RenderShaderHandle(handle)
                );
                break;
            case RenderResourceKind.Mesh:
                _gpuCache.Publish(
                    assetId,
                    _cache.Find(assetId)?.SourceRevision ?? 0UL,
                    new RenderMeshHandle(handle)
                );
                break;
            default:
                Log.Warning($"[AssetManager] 未知资源种类，跳过发布: {kind}");
                break;
        }
    }

    /// <summary>测试断言用：按资产与种类查询已登记 GPU 句柄（未登记为 0）。</summary>
    internal ulong GetRenderHandleForTests(AssetId assetId, RenderResourceKind kind) =>
        TryGetRenderHandle(assetId, kind, out var handle) ? handle : 0UL;

    /// <summary>排队 GPU 创建请求（Main 域）：Payload → 无资产语义请求 + RequestId 关联登记。</summary>
    private void QueueGpuCreation(AssetId assetId, ulong revision, IAssetPayload payload)
    {
        RenderResourceCreateRequest? request = payload switch
        {
            TextureAsset texture => _bridge.CreateTextureRequest(texture),
            ShaderAsset shader => _bridge.CreateShaderRequest(shader),
            MeshAsset mesh => _bridge.CreateMeshRequest(mesh),
            _ => null, // 非渲染载荷不产生 GPU 创建请求
        };
        if (request is null)
            return;
        var requestId = new RenderResourceRequestId(Interlocked.Increment(ref _nextRequestId));
        _gpuCache.TrackRequest(requestId, assetId, revision, request.Kind);
        _pendingCreates.Add(new RenderResourceCreateItem(requestId, request));
    }

    /// <summary>仅承接释放请求的桥接接收器（创建请求经 QueueGpuCreation 携带身份排队）。</summary>
    private sealed class ReleaseOnlySink(AssetManager owner) : IRenderRequestSink
    {
        /// <summary>创建请求不经接收器提交（由 AssetManager 携带资产身份排队）。</summary>
        public void Submit(RenderResourceCreateRequest request) =>
            throw new InvalidOperationException(
                "创建请求须经 AssetManager.QueueGpuCreation 携带资产身份提交"
            );

        /// <summary>释放请求入队（渲染线程帧首消费）。</summary>
        public void Submit(RenderResourceReleaseRequest request) =>
            owner._renderReleases.Enqueue(request);
    }

    /// <summary>帧末结果应用（Pipeline 经 FrameCommit 投递；Main 域）：更新 AssetEntry.Payload 与状态。</summary>
    /// <param name="result">管线结果</param>
    internal void ApplyPipelineResult(AssetPipelineResult result)
    {
        var entry = _cache.GetOrAdd(result.Key.AssetId);
        if (result.State == AssetPipelineResultState.Succeeded && result.Payload is not null)
        {
            entry.State = AssetState.Ready;
            entry.SourceRevision = result.Key.SourceRevision;
            _cache.SetPayload(entry, result.Payload);
            QueueGpuCreation(entry.AssetId, entry.SourceRevision, result.Payload);
        }
        else
        {
            entry.State = AssetState.Failed;
            _cache.SetPayload(entry, null);
            if (result.Error is not null)
                Log.Error(
                    $"[AssetManager] 资产构建失败 ({result.Key.AssetId}): {result.Error.Message}"
                );
        }
    }

    /// <summary>测试断言用：当前缓存</summary>
    internal AssetCache Cache => _cache;

    /// <summary>测试断言用：资产驻留持有计数（未驻留为 0）。</summary>
    internal int GetResidencyForTests(AssetId assetId) => _residency.GetValueOrDefault(assetId);

    /// <summary>测试断言用：目录登记记录数（瞬态资产不进入目录 → RegisterTransient 后恒 0）。</summary>
    internal int IndexCountForTests => (_keyResolver as AssetPipeline)?.CatalogCountForTests ?? 0;

    /// <summary>AssetId → 缓存载荷（Data 为 T 且目录修订一致才返回）；未命中、类型不符或源已失效返回 null。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已就绪载荷；未命中/类型不符/失效为 null</returns>
    public T? TryResolve<T>(AssetId assetId)
        where T : class => TryResolveUntyped(assetId) as T;

    /// <summary>按句柄解析载荷；未命中/类型不符/失效返回 false。</summary>
    /// <typeparam name="T">资产载荷类型</typeparam>
    /// <param name="handle">资产句柄</param>
    /// <param name="payload">已就绪载荷（未命中为 null）</param>
    /// <returns>解析成功为 true</returns>
    public bool TryResolve<T>(AssetHandle<T> handle, out T? payload)
        where T : class, IAssetPayload
    {
        payload = TryResolve<T>(handle.Id);
        return payload is not null;
    }

    /// <summary>AssetId → 已就绪缓存数据（目录修订一致才返回）；未命中或源已失效返回 null。类型化查询与序列化层 resolver 视图共用。</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已就绪资产数据；未命中/失效为 null</returns>
    internal IAssetPayload? TryResolveUntyped(AssetId assetId)
    {
        var entry = _cache.Find(assetId);
        if (entry is not { State: AssetState.Ready, Payload: { } data })
            return null;
        // 缓存命中只接受当前修订：目录记录存在且修订不一致说明源已失效
        if (_keyResolver.CurrentSourceRevision(assetId) != entry.SourceRevision)
            return null;
        return data;
    }

    /// <summary>取出下一个待渲染释放请求（Main 域驱逐后入队；渲染线程帧首消费）。</summary>
    /// <param name="request">释放请求（无资产语义：种类 + GPU 句柄）</param>
    /// <returns>有请求时为 true</returns>
    internal bool TryDequeueRenderRelease(out RenderResourceReleaseRequest request) =>
        _renderReleases.TryDequeue(out request);

    /// <summary>渲染线程帧首调用：排空待释放请求队列；consume 为 null 时维持占位行为（Log + 丢弃）。</summary>
    /// <param name="consume">释放请求消费回调（渲染侧接入后提供）</param>
    public void ProcessUnloadQueue(Action<RenderResourceReleaseRequest>? consume = null)
    {
        while (_renderReleases.TryDequeue(out var request))
        {
            if (consume is not null)
                consume(request);
            else if (LogConfig.Assets)
                Log.Info($"[Assets] Render release pending: {request.Kind} #{request.Handle}");
        }
    }

    /// <summary>
    /// 受控引用解析器视图：按 AssetId 从本管理器缓存解析已加载载荷（未加载/未命中返回 null，调用方据此区分加载中）。
    /// 本管理器不持有序列化记录，TryGetRecord 恒返回 null；无任何全局服务定位或静态状态。
    /// </summary>
    private sealed class CatalogReferenceResolver(AssetManager manager) : IAssetReferenceResolver
    {
        /// <summary>本管理器不持有序列化记录，恒返回 null</summary>
        public AssetSerializationRecord? TryGetRecord(AssetId assetId) => null;

        /// <summary>按强类型句柄从缓存解析已加载载荷；未加载/未命中返回 null</summary>
        public T Resolve<T>(AssetHandle<T> handle)
            where T : class => manager.TryResolveUntyped(handle.Id) as T ?? null!;

        /// <summary>按非泛型句柄从缓存解析已加载载荷；未加载/未命中返回 null</summary>
        public object Resolve(UntypedAssetHandle handle) =>
            manager.TryResolveUntyped(handle.Id) ?? null!;
    }
}
