using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SilkEngine.Render;

public static class DefaultWindowOption
{
    public static WindowOptions DefaultOpenGLOption = WindowOptions.Default with
    {
        Title = "Silk.Net Window(OpenGL)",
        Size = new Vector2D<int>(800, 600),
        API = new GraphicsAPI(ContextAPI.OpenGL, new APIVersion(4, 6)),
    };

    public static WindowOptions DefaultVulkanOption = WindowOptions.DefaultVulkan with
    {
        Title = "Silk.Net Window(Vulkan)",
        Size = new Vector2D<int>(800, 600),
        API = new GraphicsAPI(ContextAPI.Vulkan, new APIVersion(1, 3)),
    };
}
