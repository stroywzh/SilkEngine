using SilkEngine.Rendering.Abstraction;

namespace SilkEngine;

/// <summary>Host-facing application registrations and engine-wide switches.</summary>
public class Application
{
    /// <summary>Rendering backend used by the main loop.</summary>
    /// <remarks>
    /// Must be assigned before <c>ThreadFactory.StartMainThread()</c>; the main loop throws
    /// <see cref="ArgumentNullException"/> when it is still missing.
    /// </remarks>
    public static IRenderBackend RenderBackend = null!;

    /// <summary>Rendering pipeline used by the main loop.</summary>
    /// <remarks>Same assignment rule as <see cref="RenderBackend"/>.</remarks>
    public static IRenderPipeline RenderPipeline = null!;

    /// <summary>Whether the host embeds the engine instead of owning the process.</summary>
    /// <remarks>
    /// Set by the host during assembly and only read by the engine; deliberately no single writer
    /// (C-09: this flag is a host contract, not engine state).
    /// </remarks>
    public static bool IsEmbed = false;

    /// <summary>Whether the engine main loop is running.</summary>
    /// <remarks>
    /// The single writer is the <b>main-thread</b> <see cref="Runtime.EngineThread"/>: it sets the flag on
    /// the Running and Disposed transitions, and only when the thread reports <c>IsMainThread</c>
    /// (C-14/C-09 + CORE-N-05) — render threads never write it. Hosts must only read it.
    /// </remarks>
    public static bool IsRunning { get; internal set; }

    /// <summary>Module-level log switches.</summary>
    public static class LogSetting
    {
        public static bool EnableLog = true;
        public static bool EnableMainThreadLog = true;
        public static bool EnableRenderLog = true;
    }
}
