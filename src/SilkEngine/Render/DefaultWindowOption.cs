using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SilkEngine.Render;

/// <summary>默认窗口选项预设（800×600：OpenGL 4.6 / Vulkan 1.3）</summary>
public static class DefaultWindowOption
{
    /// <summary>OpenGL 4.6 窗口选项</summary>
    public static WindowOptions DefaultOpenGLOption = WindowOptions.Default with
    {
        Title = "Silk.Net Window(OpenGL)",
        Size = new Vector2D<int>(800, 600),
        API = new GraphicsAPI(ContextAPI.OpenGL, new APIVersion(4, 6)),
    };

    /// <summary>Vulkan 1.3 窗口选项</summary>
    public static WindowOptions DefaultVulkanOption = WindowOptions.DefaultVulkan with
    {
        Title = "Silk.Net Window(Vulkan)",
        Size = new Vector2D<int>(800, 600),
        API = new GraphicsAPI(ContextAPI.Vulkan, new APIVersion(1, 3)),
    };
}
