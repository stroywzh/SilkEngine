using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;

namespace SilkEngine;

public class Application
{
    internal static volatile bool _isRunning = false;
    public static bool IsRunning => _isRunning;
    public static bool IsEmbed = false;
    public static IRenderBackend RenderBackend = null!;
    public static IRenderPipeline RenderPipeline = null!;

    public static class LogSetting
    {
        public static bool EnableLog = true;
        public static bool EnableMainThreadLog = true;
        public static bool EnableRenderLog = true;
    }
}
