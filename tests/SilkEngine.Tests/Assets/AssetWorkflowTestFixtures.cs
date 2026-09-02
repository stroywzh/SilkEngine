using System.Text;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 资产工作流测试共享支持：可复现的临时目录创建与递归清理，供测试在 finally/Dispose 中使用。
/// </summary>
internal static class TestTempDirectory
{
    /// <summary>在系统临时目录下创建唯一的空子目录</summary>
    /// <returns>新目录的完整路径</returns>
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "silk-asset-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>递归删除目录及其全部内容；目录不存在时忽略</summary>
    /// <param name="path">待删除的目录路径</param>
    public static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

/// <summary>
/// 资产工作流共享测试数据：固定字节/内容常量（确定性，不读仓库外文件）。
/// ValidPng/SecondPng 均为真实可解码的 1×1 PNG（ValidPng 与 PngFixtures.RedPng 同字节）。
/// </summary>
public static class TestAssetData
{
    public const string ValidCubeObj = "v 0 0 0\nv 1 0 0\nv 0 1 0\n"
        + "vt 0 0\nvt 1 0\nvt 0 1\n"
        + "vn 0 0 1\nf 1/1/1 2/2/1 3/3/1\n";

    /// <summary>1×1 红色 RGBA PNG（嵌入式 base64 常量，双解码器实测可解码）</summary>
    public static byte[] ValidPng { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==");

    /// <summary>与 ValidPng 内容不同的第二个有效 1×1 PNG（嵌入式 base64 常量）</summary>
    public static byte[] SecondPng { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAEE0AABBNAWeMAeAAAAANSURBVBhXY2Bg+P8fAAMCAf/Jsq3uAAAAAElFTkSuQmCC");

    /// <summary>不可解码的坏 PNG 字节（解码器应失败）</summary>
    public static byte[] InvalidPng => [0, 1, 2, 3];

    /// <summary>合法 HLSL 源（含 vert→SV_Position 与 frag→SV_Target 两个入口函数）</summary>
    internal const string UnlitShaderSource =
        "float4 vert(float3 position : POSITION) : SV_Position { return float4(position, 1.0); }\n"
        + "float4 frag() : SV_Target { return float4(1.0, 1.0, 1.0, 1.0); }\n";

    /// <summary>材质 JSON：引用 shader/texture/mesh 三个路径 + BaseColor 参数</summary>
    internal const string MaterialJson = "{\"schema\":1,\"type\":\"material\","
        + "\"shader\":\"Shaders/Unlit.hlsl\","
        + "\"texture\":\"Textures/ShoreKeeper1.png\","
        + "\"mesh\":\"Meshes/Cube.obj\","
        + "\"parameters\":{\"BaseColor\":[1,1,1]}}";
}

/// <summary>
/// 磁盘资产管线测试夹具：临时 AssetRoot 写入真实文件 → <see cref="AssetManager.CreateDiskBacked"/> 装配
/// 真实 Pipeline + AssetDB（数据库位于临时 Library 目录），支持端到端路径依赖解析。
/// LoadAsync 同步等待底层管线解算完成再返回已完成操作（测试线程无持续心跳，await 即取即得）；
/// Compatible 后续任务（Blocking 读取门控 / WithMutableFile+Replace 变更检测）。
/// </summary>
public sealed class TestAssetPipelineFixture : IDisposable
{
    internal const string ProjectNamespace = "sandbox";

    private readonly string _tempRoot;
    private readonly ThreadRuntime _runtime;
    private readonly GatedAssetFileSystem? _gate;

    private TestAssetPipelineFixture(string tempRoot, ThreadRuntime runtime, AssetManager manager, AssetPipeline pipeline, GatedAssetFileSystem? gate)
    {
        _tempRoot = tempRoot;
        _runtime = runtime;
        _gate = gate;
        Manager = manager;
        Pipeline = pipeline;
    }

    /// <summary>资产门面（真实磁盘管线 + SQLite AssetDB）</summary>
    public AssetManager Manager { get; }

    /// <summary>内部管线（测试断言依赖索引/数据库用）</summary>
    internal AssetPipeline Pipeline { get; }

    /// <summary>线程运行时（FrameCommit 排空用）</summary>
    internal ThreadRuntime Runtime => _runtime;

    /// <summary>创建临时资产根目录并写入给定文件（Type 取值 shader/texture/mesh/material）</summary>
    /// <param name="files">逻辑路径 + 类型元组列表</param>
    /// <returns>已装配完整磁盘管线的夹具</returns>
    public static TestAssetPipelineFixture CreateWith(params (string Path, string Type)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var tempRoot = TestTempDirectory.Create();
        var runtime = CreateRuntimeOnCurrentThread();
        try
        {
            foreach (var (path, type) in files)
                WriteBytes(tempRoot, path, ContentFor(type));
            return Build(tempRoot, runtime, files: null);
        }
        catch
        {
            runtime.Dispose();
            try { TestTempDirectory.Delete(tempRoot); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>创建单文件夹具并门控该路径读取：LoadAsync 挂起直到 <see cref="ReleaseRead"/>（后续任务变更检测用）</summary>
    /// <param name="path">被门控的逻辑路径（须属内置类型扩展名）</param>
    /// <returns>读取挂起的夹具</returns>
    public static TestAssetPipelineFixture Blocking(string path)
    {
        var tempRoot = TestTempDirectory.Create();
        var runtime = CreateRuntimeOnCurrentThread();
        try
        {
            WriteBytes(tempRoot, path, ContentFor(ContentTypeOf(path)));
            return Build(tempRoot, runtime, new GatedAssetFileSystem(tempRoot, path));
        }
        catch
        {
            runtime.Dispose();
            try { TestTempDirectory.Delete(tempRoot); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>创建单文件夹具并提供 <see cref="Replace"/> 重写入口（后续任务变更检测用）</summary>
    /// <param name="path">逻辑路径</param>
    /// <param name="contents">初始文件文本内容</param>
    /// <returns>已装配磁盘管线的夹具</returns>
    public static TestAssetPipelineFixture WithMutableFile(string path, string contents)
    {
        var tempRoot = TestTempDirectory.Create();
        var runtime = CreateRuntimeOnCurrentThread();
        try
        {
            WriteText(tempRoot, path, contents);
            return Build(tempRoot, runtime, files: null);
        }
        catch
        {
            runtime.Dispose();
            try { TestTempDirectory.Delete(tempRoot); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>加载资产：阻塞至管线解算完成，返回已完成的安全操作（await 不再依赖 FrameCommit 派发）</summary>
    /// <typeparam name="TPayload">资产载荷类型</typeparam>
    /// <param name="path">资产逻辑路径（相对临时资产根目录）</param>
    /// <returns>已完成的安全资产操作</returns>
    public AssetOperation<TPayload> LoadAsync<TPayload>(string path)
        where TPayload : class, IAssetPayload
    {
        var operation = Manager.LoadAsync<TPayload>(path);
        var payload = operation.AsTask().GetAwaiter().GetResult();
        return Manager.WrapExternalTask(Task.FromResult(payload));
    }

    /// <summary>释放 Blocking 门的读取挂起（非 Blocking 夹具下为空操作）</summary>
    public void ReleaseRead() => _gate?.Release();

    /// <summary>重写指定逻辑路径的物理文件内容（文件不存在时抛 FileNotFoundException）</summary>
    /// <param name="path">逻辑路径</param>
    /// <param name="contents">新文件内容</param>
    public void Replace(string path, string contents)
    {
        var physical = ToPhysical(path);
        if (!File.Exists(physical))
            throw new FileNotFoundException($"文件不存在：{path}", path);
        File.WriteAllText(physical, contents);
    }

    /// <summary>排空 FrameCommit 阶段（测试线程已登记 Main 域）：执行依赖持久化与结果应用</summary>
    internal void DrainFrameCommit() => _runtime.Drain(MainThreadPhase.FrameCommit);

    /// <summary>释放：关闭管理器（含 AssetDB）与运行时，删除临时目录</summary>
    public void Dispose()
    {
        Manager.Dispose();
        _runtime.Dispose();
        TestTempDirectory.Delete(_tempRoot);
    }

    private static ThreadRuntime CreateRuntimeOnCurrentThread()
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        return runtime;
    }

    private static TestAssetPipelineFixture Build(string tempRoot, ThreadRuntime runtime, IAssetFileSystem? files)
    {
        var gate = files as GatedAssetFileSystem;
        var manager = AssetManager.CreateDiskBacked(
            tempRoot,
            runtime,
            files: files,
            projectNamespace: ProjectNamespace,
            libraryRoot: Path.Combine(tempRoot, "Library"));
        var pipeline = manager.PipelineForTests
            ?? throw new InvalidOperationException("CreateDiskBacked 应返回 AssetPipeline");
        return new TestAssetPipelineFixture(tempRoot, runtime, manager, pipeline, gate);
    }

    /// <summary>按逻辑路径扩展名推断内容类型（Blocking 等按路径构造文件的入口）</summary>
    private static string ContentTypeOf(string logicalPath) => Path.GetExtension(logicalPath).ToLowerInvariant() switch
    {
        ".hlsl" => "shader",
        ".png" or ".jpg" => "texture",
        ".obj" => "mesh",
        ".asset" => "material",
        _ => throw new ArgumentException($"路径 '{logicalPath}' 的扩展名没有内置内容模板", nameof(logicalPath)),
    };

    private static byte[] ContentFor(string type)
    {
        var content = type.ToLowerInvariant() switch
        {
            "shader" => Encoding.UTF8.GetBytes(TestAssetData.UnlitShaderSource),
            "texture" => TestAssetData.ValidPng,
            "mesh" => Encoding.UTF8.GetBytes(TestAssetData.ValidCubeObj),
            "material" => Encoding.UTF8.GetBytes(TestAssetData.MaterialJson),
            _ => throw new ArgumentException($"未知资产类型 '{type}'（支持 shader/texture/mesh/material）", nameof(type)),
        };
        return content;
    }

    private static void WriteBytes(string root, string logicalPath, byte[] content) =>
        WriteFile(root, logicalPath, stream => stream.Write(content));

    private static void WriteText(string root, string logicalPath, string text) =>
        WriteFile(root, logicalPath, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(text);
            writer.Flush();
        });

    private static void WriteFile(string root, string logicalPath, Action<FileStream> write)
    {
        var physical = Path.Combine(root, NormalizeForDisk(logicalPath));
        var directory = Path.GetDirectoryName(physical);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var stream = new FileStream(physical, FileMode.Create, FileAccess.Write, FileShare.Read);
        write(stream);
    }

    private string ToPhysical(string logicalPath) =>
        Path.Combine(_tempRoot, NormalizeForDisk(logicalPath));

    private static string NormalizeForDisk(string logicalPath) =>
        logicalPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}

/// <summary>
/// 读取门控文件系统：包装磁盘文件服务，指定逻辑路径的读取挂起直到 <see cref="Release"/>（测试夹具）。
/// </summary>
internal sealed class GatedAssetFileSystem : IAssetFileSystem
{
    private readonly DiskAssetFileSystem _inner;
    private readonly string _gatedLogicalPath;
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GatedAssetFileSystem(string root, string gatedLogicalPath)
    {
        _inner = new DiskAssetFileSystem(root);
        _gatedLogicalPath = _inner.Normalize(gatedLogicalPath);
    }

    /// <summary>释放门控路径的读取挂起（幂等）</summary>
    public void Release() => _gate.TrySetResult();

    public string Normalize(string path) => _inner.Normalize(path);

    public bool Exists(string path) => _inner.Exists(path);

    public ValueTask<FileMetadata> GetMetadataAsync(string path) => _inner.GetMetadataAsync(path);

    public ScanResult Scan() => _inner.Scan();

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path)
    {
        if (_inner.Normalize(path) == _gatedLogicalPath)
            await _gate.Task.ConfigureAwait(false);
        return await _inner.ReadAsync(path);
    }
}

/// <summary>
/// 资产生命周期测试夹具：场上已就绪一个真实 GPU 句柄的纹理资产
/// （内存文件 → 同步管线加载 → Headless 渲染线程创建 → ApplyCreateResults 发布句柄）。
/// 供帧末驱逐（UnloadUnused → GPU release 入队）与关闭顺序测试使用。
/// </summary>
public sealed class AssetManagerTestFixture : IDisposable
{
    private readonly ThreadRuntime _runtime;

    private AssetManagerTestFixture(AssetManager manager, ThreadRuntime runtime, AssetHandle<TextureAsset> handle)
    {
        Manager = manager;
        _runtime = runtime;
        Handle = handle;
    }

    /// <summary>资产管理器（内建内存文件系统 + 同步管线 + Headless 渲染句柄）</summary>
    public AssetManager Manager { get; }

    /// <summary>场上已加载纹理资产的稳定句柄（Ready + GPU 句柄已发布）</summary>
    public AssetHandle<TextureAsset> Handle { get; }

    /// <summary>
    /// 创建就绪纹理夹具：加载 → FrameCommit 应用（Ready + 排队 GPU 创建）
    /// → Headless 渲染线程消费创建批次 → Main 域发布 GPU 句柄。
    /// </summary>
    /// <returns>已就绪的资产管理器夹具</returns>
    public static AssetManagerTestFixture ReadyTexture()
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("T.png", PngFixtures.RedPng);
        var context = TestAssetPipeline.CreateContext(files, index =>
            index.Apply(ScanResult.FromFiles([ScanFile.File("T.png", 1)])));
        context.Manager.Load<TextureAsset>("T.png");
        context.Runtime.Drain(MainThreadPhase.FrameCommit);
        var entry = context.Manager.Cache.All().Single(e => e.State == AssetState.Ready);
        var handle = new AssetHandle<TextureAsset>(entry.AssetId);
        var renderHost = new RenderThreadHost(runtime, new HeadlessRenderBackend());
        runtime.RegisterManagedLoop(renderHost);
        renderHost.Start();
        renderHost.SubmitFrame(new RenderSubmission(
            FrameCameraBlock.Identity, [], context.Manager.DrainCreateBatch()));
        context.Manager.ApplyCreateResults(renderHost.LastCreateResults);
        return new AssetManagerTestFixture(context.Manager, runtime, handle);
    }

    /// <summary>释放：关闭管理器（取消在途作业、丢弃结果、注销服务）→ 停止渲染线程与运行时</summary>
    public void Dispose()
    {
        Manager.Dispose();
        _runtime.Dispose();
    }
}