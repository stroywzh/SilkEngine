using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine;

/// <summary>Host-facing application registrations and engine-wide switches.</summary>
public class Application
{
    public static Vector2Int WindowInitialSize = new Vector2Int(800, 600);

    /// <summary>Rendering backend used by the main loop.</summary>
    public static IRenderBackend RenderBackend = null!;

    /// <summary>Rendering pipeline used by the main loop.</summary>
    public static IRenderPipeline RenderPipeline = null!;

    /// <summary>Whether the host embeds the engine instead of owning the process.</summary>
    public static bool IsEmbed = false;

    /// <summary>Whether the engine main loop is running.</summary>
    public static bool IsRunning { get; internal set; }

    /// <summary>Registry of asset types to loaders; frozen after first use.</summary>
    /// <remarks>Registered by the content bootstrap before startup; not part of the application surface.</remarks>
    internal static Dictionary<Type, IAssetLoader> AssetLoaders { get;set; } =
        new Dictionary<Type, IAssetLoader> { };

    /// <summary>Asset worker tuning; read when the worker starts or when a request is submitted.</summary>
    public static class AssetSetting
    {
        /// <summary>Grace window between a refcount reaching zero and the asset being unregistered.</summary>
        public static TimeSpan DeferredReleaseWindow = TimeSpan.FromMilliseconds(1000);

        /// <summary>Maximum number of queued load requests; submitting beyond it throws instead of growing without bound.</summary>
        public static int MaxPendingRequests = 1024;

        /// <summary>Number of decoding tasks allowed to run on the thread pool at the same time.</summary>
        public static int DecodeConcurrency = Math.Clamp(Environment.ProcessorCount - 1, 1, 4);

        /// <summary>Maximum number of completed requests the main loop pumps per frame.</summary>
        public static int CompletionPumpBudget = 256;

        /// <summary>Upper bound the asset worker waits for in-flight decodes while shutting down.</summary>
        public static TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);
    }

    /// <summary>Module-level log switches.</summary>
    public static class LogSetting
    {
        /// <summary>Master switch; when false, nothing is written.</summary>
        public static bool EnableLog = true;

        /// <summary>Whether the main loop writes its frame/stop messages.</summary>
        public static bool EnableMainThreadLog = true;

        /// <summary>Whether the render side writes per-frame upload/resize messages.</summary>
        public static bool EnableRenderLog = true;
    }
}
