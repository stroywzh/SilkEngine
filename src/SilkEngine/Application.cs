using SilkEngine.Rendering.Abstraction;

namespace SilkEngine;

public class Application
{
    internal static volatile bool _isRunning = false;
    public static bool IsRunning => _isRunning;
    public static bool IsEmbed = false;
    public static IRenderBackend RenderBackend;
    public static IRenderPipeline RenderPipeline;
    public static class LogSetting
    {
        public static bool EnableLog = true;
        public static bool EnableMainThreadLog = true;
        public static bool EnableRenderLog = true;
    }
}
