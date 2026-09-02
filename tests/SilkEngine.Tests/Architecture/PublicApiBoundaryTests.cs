using SilkEngine.Assets;
using SilkEngine.Rendering.Pipeline;

namespace SilkEngine.Tests.Architecture;

/// <summary>
/// 阶段 4 任务 3：公开面收敛 —— 编排类型（管线/收集器）不得为 public；
/// 死代码（AssetRequest、SQL 存储桩）与 NotImplementedException 不得存在于生产源码。
/// </summary>
public class PublicApiBoundaryTests
{
    [Fact]
    public void InternalOrTestOnlyTypes_AreNotPublic()
    {
        Assert.False(typeof(AssetPipeline).IsPublic);
        Assert.False(typeof(RenderCollector).IsPublic);
        Assert.False(typeof(ForwardPipeline).IsPublic);
    }

    [Fact]
    public void RemovedDeadTypes_DoNotExistInProductionSource()
    {
        var files = Directory.EnumerateFiles(FindSourceRoot(), "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("AssetRequest", source);
            Assert.DoesNotContain("SqlAssetSerializerStore", source);
            Assert.DoesNotContain("NotImplementedException", source);
        }
    }

    private static string FindSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SilkEngine.Runtime")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src");
    }
}
