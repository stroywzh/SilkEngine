using SilkEngine.Scene;

namespace SilkEngine.Tests.Architecture;

/// <summary>
/// 阶段 4 任务 2：EngineLoop 不查询具体渲染器/相机类型（场景渲染查询抽离至 SceneRenderWorld）；
/// SceneRenderWorld 从帧快照构建只读相机/渲染器源快照。
/// </summary>
public class EngineLoopBoundaryTests
{
    [Fact]
    public void EngineLoop_DoesNotKnowConcreteRendererTypes()
    {
        var source = File.ReadAllText(FindSource("EngineLoop.cs"));

        Assert.DoesNotContain("GetComponents<MeshRenderer>", source);
        Assert.DoesNotContain("GetComponents<Camera>", source);
        Assert.DoesNotContain("MeshRenderer", source);
        Assert.DoesNotContain("Camera", source);
    }

    [Fact]
    public void SceneRenderWorld_ProvidesCameraAndRendererSnapshot()
    {
        var world = new SceneRenderWorld(new FrameSnapshotManager(), []);

        var snapshot = world.BuildSnapshot();

        Assert.NotNull(snapshot.Cameras);
        Assert.NotNull(snapshot.Renderers);
        Assert.Single(snapshot.Cameras); // 默认相机回退
    }

    private static string FindSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SilkEngine")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Directory.EnumerateFiles(
                Path.Combine(dir.FullName, "src", "SilkEngine"),
                fileName,
                SearchOption.AllDirectories)
            .First();
    }
}
