using System;
using System.IO;
using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Host;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 瞬态资产契约：RegisterTransient 不经 VFS/目录（无目录记录），Payload 立即就绪并返回稳定句柄；
/// Sandbox Demo 正式展示路径经静态 Assets 门面访问磁盘资产，瞬态构造（DemoAssetsExt）保留给程序化辅助，
/// 均禁止自造随机 ID 或直接构造配置 Handle。
/// </summary>
[Collection("Assets")]
public class TransientAssetTests : IDisposable
{
    private readonly EngineHost _host;

    public TransientAssetTests()
    {
        _host = EngineHost.Create(b => b.UseHeadlessForTests());
        _host.Initialize();
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void RegisterTransient_ReturnsStableHandleAndDoesNotUseVfs()
    {
        var payload = new MeshAsset("cube", new float[] { 0, 1, 2 }, new[] { 3 }, null);

        var handle = _host.AssetManager.RegisterTransient(payload);

        Assert.NotEqual(default, handle);
        Assert.True(_host.AssetManager.TryResolve(handle, out MeshAsset? resolved));
        Assert.Same(payload, resolved);
        Assert.Equal(0, _host.AssetManager.IndexCountForTests);
    }

    [Fact]
    public void SandboxSource_UsesStaticFacadeAndNeverCreatesRandomHandle()
    {
        var source = File.ReadAllText(FindSource("TestSingleCube.cs"));

        // 任务 11：正式展示路径迁移到静态 Assets 门面（磁盘资产），瞬态构造退出主链路
        Assert.Contains("Assets.Load", source);
        Assert.Contains("Materials/Cube.asset", source);
        Assert.DoesNotContain("Guid.NewGuid", source);
        Assert.DoesNotContain("new AssetHandle", source);
        Assert.DoesNotContain("IRenderDevice", source);
        Assert.DoesNotContain("DemoAssets.NewId", source);
    }

    [Fact]
    public void AllDemoSources_UseTransientAssetsWithoutFakeHandles()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sandbox/Demos"));
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).ToList();

        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "DemoAssets.cs");

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("DemoAssets.NewId", source);
            Assert.DoesNotContain("Guid.NewGuid", source);
            Assert.DoesNotContain("new AssetHandle", source);
            Assert.DoesNotContain("IRenderDevice", source);
            Assert.DoesNotContain("GPU 句柄待创建请求接线后发布", source);
        }
    }

    private static string FindSource(string fileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sandbox"));
        var file = Directory.GetFiles(root, fileName, SearchOption.AllDirectories).SingleOrDefault()
            ?? throw new FileNotFoundException($"{fileName} 未在 Sandbox 目录找到");
        return file;
    }
}