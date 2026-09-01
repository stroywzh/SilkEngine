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
        Demos.TestSingleCube.Run(host);
        // Demos.TestNDCTriangle.Run(host);
        // Demos.TestNDCQuad.Run(host);
        // Demos.TestCameraOrtho.Run(host);
        // Demos.TestCameraPerspective.Run(host);
        // Demos.TestPNGQuad.Run(host);
        // Demos.TestThirdPerson3D.Run(host);
        // Demos.IMGShow.Run(host);
        // ---------------------------------------------------------

        host.Run();
    }
}
