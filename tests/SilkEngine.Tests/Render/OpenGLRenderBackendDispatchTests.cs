using SilkEngine.Render;
using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

// B.1 单点分派测试（批次 B，G4 #1）
// 可测性说明：ExecuteCommands 依赖真实 GL 上下文（_gl==null 直接返回；Silk.NET GL 为具体类、
// 测试环境无头无法创建上下文），GL 调用级计数（Draw/DrawInstanced 实际调用次数）无法无头测试
// —— 按 PLAN B.1 豁免，以代码审查 + 分支结构断言（grep 单点 switch）替代。
// 分派决策本身为纯逻辑（internal OpenGLRenderBackend.Classify，无 GL 依赖），在此无头测试。
public class OpenGLRenderBackendDispatchTests
{
    [Fact]
    public void SingleCommand_ClassifiesDrawOnce()
    {
        var cmd = new SingleDrawCommand();

        var kind = OpenGLRenderBackend.Classify(cmd);

        Assert.Equal(DrawCommandKind.DrawOnce, kind);
    }

    [Fact]
    public void InstancedCommand_ClassifiesDrawInstanced()
    {
        var cmd = new InstancedDrawCommand { InstanceCount = 42 };

        var kind = OpenGLRenderBackend.Classify(cmd);

        Assert.Equal(DrawCommandKind.DrawInstanced, kind);
    }

    [Fact]
    public void UnknownCommandType_ClassifiesUnknown()
    {
        var cmd = new CustomCommand();

        var kind = OpenGLRenderBackend.Classify(cmd);

        Assert.Equal(DrawCommandKind.Unknown, kind);
    }

    private sealed class CustomCommand : DrawCommand { }
}
