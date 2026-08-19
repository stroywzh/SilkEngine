using SilkEngine.Core;
using SilkEngine.InputSystem;
using SilkEngine.Render.OpenGL;

namespace SandBox;

class Program
{
    static void Main(string[] args)
    {
        var backend = new OpenGLRenderBackend();
        var engine = new EngineLoop(backend);
        Input.EnableLog = true;
        LogConfig.Render = false;

        // -------------------- 逐个取消注释测试 --------------------
        // Demos.TestSingleCube.Run(engine);
        // Demos.TestNDCTriangle.Run(engine);
        // Demos.TestNDCQuad.Run(engine);
        // Demos.TestCameraOrtho.Run(engine);
        // Demos.TestCameraPerspective.Run(engine);
        // Demos.TestPNGQuad.Run(engine);
        Demos.TestThirdPerson3D.Run(engine);
        // Demos.TestPNGQuad.Run(engine);
        // ---------------------------------------------------------

        engine.Initialize().Run();
    }
}
