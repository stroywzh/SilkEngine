using SilkEngine.Assets;
using SilkEngine.Assets.Binding;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Host;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 资产热重载（任务 10）：EngineLoop 低频扫描槽 + 变更源收敛 + 扫描对账（指纹）+
/// 级联失效（AssetDependencyIndex.InvalidateCascade 接线）+ 新版本发布语义
/// （成功前保留旧载荷，失败保留上一版就绪载荷并记录错误）。
/// HostAssetFixture（private）装配 Headless Host + 临时磁盘 AssetRoot + 内存变更源。
/// </summary>
[Collection("Assets")]
public sealed class AssetHotReloadTests
{
    [Fact]
    public async Task ModifiedTexture_ReimportsAndPublishesNewRevision()
    {
        using var fixture = HostAssetFixture.WithAssets();
        var oldTexture = await fixture.LoadAsync<TextureAsset>("Textures/ShoreKeeper1.png");
        var oldRevision = fixture.GetRevision("Textures/ShoreKeeper1.png");

        fixture.Change("Textures/ShoreKeeper1.png", TestAssetData.SecondPng);
        fixture.StepFrames(3);

        Assert.True(fixture.GetRevision("Textures/ShoreKeeper1.png") > oldRevision);
        Assert.NotEqual(oldTexture, fixture.Resolve<TextureAsset>("Textures/ShoreKeeper1.png"));
    }

    [Fact]
    public void FailedReload_KeepsPreviousReadyPayload()
    {
        using var fixture = HostAssetFixture.WithAssets();
        var old = fixture.Resolve<TextureAsset>("Textures/ShoreKeeper1.png");

        Assert.NotNull(old); // WithAssets 预加载参考纹理并保有驻留槽，保证断言有意义
        fixture.Change("Textures/ShoreKeeper1.png", TestAssetData.InvalidPng);
        fixture.StepFrames(3);

        Assert.Same(old, fixture.Resolve<TextureAsset>("Textures/ShoreKeeper1.png"));
        Assert.Contains("Textures/ShoreKeeper1.png", fixture.LastAssetError);
    }

    /// <summary>
    /// 宿主级资产夹具（private）：Headless Host + 临时磁盘 AssetRoot（含参考纹理）+ 内存变更源。
    /// 装配即预加载参考纹理并保有驻留槽（帧末遗留驱逐的前提），步进到缓存条目 Ready；
    /// <see cref="Change"/> 重写物理文件并推事件，对账以重扫指纹为准（变更事件仅作触发信号，可幂等）。
    /// <see cref="StepFrames"/> 在给定帧数之上追加重载稳定步（在途重建结果落账为止），
    /// 规避测试线程帧步进快于 ThreadPool 重建 Worker 的调度抖动（生产帧率下无此问题）。
    /// </summary>
    private sealed class HostAssetFixture : IDisposable
    {
        private const string ReferenceTexture = "Textures/ShoreKeeper1.png";

        private readonly string _tempRoot;
        private readonly string _assetRoot;
        private readonly EngineHost _host;
        private readonly MemoryAssetChangeSource _changeSource;
        private AssetSlot<TextureAsset> _referenceSlot = null!;

        private HostAssetFixture(string tempRoot, string assetRoot, EngineHost host, MemoryAssetChangeSource changeSource)
        {
            _tempRoot = tempRoot;
            _assetRoot = assetRoot;
            _host = host;
            _changeSource = changeSource;
        }

        /// <summary>创建预加载参考纹理的宿主夹具（Headless + 磁盘管线 + 内存变更源，扫描间隔为 0 逐帧探测）</summary>
        public static HostAssetFixture WithAssets()
        {
            var tempRoot = TestTempDirectory.Create();
            var assetRoot = Path.Combine(tempRoot, "AssetRoot");
            Directory.CreateDirectory(assetRoot);
            WriteFile(assetRoot, ReferenceTexture, TestAssetData.ValidPng);

            var changeSource = new MemoryAssetChangeSource();
            var host = EngineHost.Create(builder =>
            {
                builder.UseHeadlessForTests();
                builder.UseAssetRoot(assetRoot);
                builder.UseLibraryRoot(Path.Combine(tempRoot, "Library"));
                builder.UseAssetChangeScanIntervalForTests(TimeSpan.Zero);
                builder.UseAssetChangeSourceForTests(changeSource);
            });
            host.Initialize();
            try
            {
                var fixture = new HostAssetFixture(tempRoot, assetRoot, host, changeSource);
                // 预加载参考纹理并保有驻留槽：帧末驱逐不回收（重载失败保留上一版就绪载荷的前提），步进到目录修订就位
                fixture.LoadAsync<TextureAsset>(ReferenceTexture).GetAwaiter().GetResult();
                fixture._referenceSlot = fixture._host.AssetManager.CreateSlot(
                    fixture._host.AssetManager.GetHandle<TextureAsset>(ReferenceTexture));
                fixture.StepFrames(3);
                return fixture;
            }
            catch
            {
                host.Dispose();
                try { TestTempDirectory.Delete(tempRoot); } catch (IOException) { }
                throw;
            }
        }

        /// <summary>异步加载资产（返回载荷；等待共享作业完成）</summary>
        public async Task<T> LoadAsync<T>(string path)
            where T : class, IAssetPayload
        {
            var operation = _host.AssetManager.LoadAsync<T>(path);
            return await operation.AsTask();
        }

        /// <summary>当前可解析载荷：缓存未就绪时同步加载；重载失败由缓存条目状态判定（不抛）</summary>
        public T? Resolve<T>(string path)
            where T : class, IAssetPayload
        {
            var manager = _host.AssetManager;
            var handle = manager.GetHandle<T>(path);
            if (manager.TryResolve(handle, out T? payload))
                return payload;
            try
            {
                return manager.Load<T>(path);
            }
            catch (Exception)
            {
                // 失败重载路径：从缓存条目返回上一版载荷（不存在或未登记时为 null）
                return manager.Cache.Find(handle.Id)?.Payload as T;
            }
        }

        /// <summary>当前目录源修订（目录未登记时 0）</summary>
        public ulong GetRevision(string path)
        {
            var id = _host.AssetManager.GetHandle<TextureAsset>(path).Id;
            return _host.AssetManager.PipelineForTests?.CurrentSourceRevision(id) ?? 0UL;
        }

        /// <summary>最近一次资产构建/重载失败的错误消息（含失败路径；未失败为 null）</summary>
        public string? LastAssetError => _host.AssetManager.LastAssetErrorForTests;

        /// <summary>重写源文件内容并投递修改变更事件（对账以重新扫描的指纹为准）</summary>
        public void Change(string path, byte[] content)
        {
            var physical = Path.Combine(_assetRoot, path.Replace('/', Path.DirectorySeparatorChar));
            using var stream = new FileStream(physical, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(content);
            _changeSource.NotifyChanged(AssetChangeKind.Modified, path);
        }

        /// <summary>步进指定数量的帧，并在参考纹理存在未落账重建时追加稳定步（封顶防挂起）</summary>
        public void StepFrames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _host.Loop.StepFrame();
                Thread.Yield();
            }

            var manager = _host.AssetManager;
            var pipeline = manager.PipelineForTests;
            if (pipeline is null)
                return;
            var id = manager.GetHandle<TextureAsset>(ReferenceTexture).Id;
            var expectedRevision = pipeline.CurrentSourceRevision(id);
            var entry = manager.Cache.Find(id);
            for (var i = 0;
                 i < 50
                 && entry is not null
                 && entry.SourceRevision != expectedRevision
                 && LastAssetError is null;
                 i++)
            {
                Thread.Sleep(5);
                _host.Loop.StepFrame();
            }
        }

        public void Dispose()
        {
            _referenceSlot.Dispose();
            _host.Dispose();
            try
            {
                TestTempDirectory.Delete(_tempRoot);
            }
            catch (IOException)
            {
            }
        }

        private static void WriteFile(string root, string logicalPath, byte[] content)
        {
            var physical = Path.Combine(root, logicalPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
            File.WriteAllBytes(physical, content);
        }
    }

    /// <summary>内存变更源（测试触发）：事件入队，Poll 排空为变更快照</summary>
    private sealed class MemoryAssetChangeSource : IAssetChangeSource
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<AssetChangeEvent> _events = new();

        public void NotifyChanged(AssetChangeKind kind, string path) => _events.Enqueue(new AssetChangeEvent(kind, path));

        public ChangeSourceResult Poll()
        {
            if (_events.IsEmpty)
                return ChangeSourceResult.Empty;
            var changes = new List<AssetChangeEvent>();
            while (_events.TryDequeue(out var change))
                changes.Add(change);
            return new ChangeSourceResult(changes);
        }
    }
}