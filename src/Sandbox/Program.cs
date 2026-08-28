using SilkEngine.Core;
using SilkEngine.Host;
using SilkEngine.InputSystem;

namespace SandBox;

class Program
{
    static void Main(string[] args)
    {
        using var host = EngineHost.Create(builder =>
        {
            builder.UseOpenGL();
            builder.UseAssetRoot("Assets");
        });
        Input.EnableLog = true;
        LogConfig.Render = false;
        host.Initialize();

        // -------------------- 逐个取消注释测试 --------------------
        Demos.TestSingleCube.Run(host.Loop);
        // Demos.TestNDCTriangle.Run(host.Loop);
        // Demos.TestNDCQuad.Run(host.Loop);
        // Demos.TestCameraOrtho.Run(host.Loop);
        // Demos.TestCameraPerspective.Run(host.Loop);
        // Demos.TestPNGQuad.Run(host.Loop);
        // Demos.TestThirdPerson3D.Run(host.Loop);
        // Demos.TestPNGQuad.Run(host.Loop);
        // Demos.IMGShow.Run(host.Loop);
        // ---------------------------------------------------------

        host.Run();
    }
}