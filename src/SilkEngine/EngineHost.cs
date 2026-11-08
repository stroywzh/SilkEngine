using SilkEngine.Asset;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;

namespace SilkEngine;

/// <summary>Startup configuration for <see cref="EngineHost"/>.</summary>
/// <remarks>The project root is fixed to <see cref="AppContext.BaseDirectory"/>; content is the
/// <c>Assets</c> folder under it (falling back to the working directory when the output has no assets).</remarks>
public sealed class EngineOptions
{
    /// <summary>Number of frames <see cref="EngineApp.Run"/> waits for before returning.</summary>
    public ulong Frames { get; init; } = 180;

    /// <summary>Whether a failed content bootstrap stops startup (<see cref="AssetStartupMode.Strict"/>)
    /// or is only logged (<see cref="AssetStartupMode.BestEffort"/>).</summary>
    public AssetStartupMode StartupMode { get; init; } = AssetStartupMode.BestEffort;

    /// <summary>Explicit project/content root; when omitted the build output (then the working directory) is used.</summary>
    public string? ProjectRoot { get; init; }

    /// <summary>Content bootstrap supplied by the composition root (Program); omit for a contentless engine.</summary>
    public IAssetBootstrap? AssetBootstrap { get; init; }

    /// <summary>Rendering backend to start with; when omitted the current <see cref="Application.RenderBackend"/> is used.</summary>
    public IRenderBackend? RenderBackend { get; init; }

    /// <summary>Rendering pipeline to start with; when omitted the current <see cref="Application.RenderPipeline"/> is used.</summary>
    public IRenderPipeline? RenderPipeline { get; init; }
}

/// <summary>Starts the engine with the content bootstrap configured, and stops it when disposed.</summary>
public static class EngineHost
{
    // Bootstrap decodes and imports content on the asset thread; the wait only bounds a hung startup.
    private static readonly TimeSpan BootstrapTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Resolves the registered backend/pipeline, runs the content bootstrap, and starts the main loop.</summary>
    /// <param name="options">Startup options; defaults are used when omitted.</param>
    /// <returns>The running engine; dispose it to stop every engine thread.</returns>
    /// <exception cref="InvalidOperationException">A backend/pipeline is missing, or strict bootstrap failed.</exception>
    /// <exception cref="TimeoutException">Strict startup did not finish within the bootstrap timeout.</exception>
    public static EngineApp Start(EngineOptions? options = null)
    {
        options ??= new EngineOptions();

        IRenderBackend backend =
            options.RenderBackend
            ?? Application.RenderBackend
            ?? throw new InvalidOperationException(
                "EngineHost.Start() needs a render backend (RenderBackend option or Application.RenderBackend)."
            );

        IRenderPipeline pipeline =
            options.RenderPipeline
            ?? Application.RenderPipeline
            ?? throw new InvalidOperationException(
                "EngineHost.Start() needs a render pipeline (RenderPipeline option or Application.RenderPipeline)."
            );

        Application.RenderBackend = backend;
        Application.RenderPipeline = pipeline;

        string projectRoot = options.ProjectRoot ?? ResolveProjectRoot();
        Log.Info($"project root: {projectRoot}");

        AssetRuntime.Bootstrap.Configure(
            options.AssetBootstrap,
            new AssetStartupContext(projectRoot, options.StartupMode)
        );

        MainLoop mainLoop = ThreadFactory.StartMainThread();

        bool completed = AssetRuntime.Bootstrap.Wait(BootstrapTimeout, out Exception? failure);
        if (!completed)
        {
            var timeout = new TimeoutException(
                $"Asset startup did not complete within {BootstrapTimeout.TotalSeconds:0} s."
            );
            if (options.StartupMode == AssetStartupMode.Strict)
            {
                ThreadRegistry.StopMainThread();
                throw timeout;
            }

            Log.Warning(timeout.Message);
        }
        else if (failure is not null)
        {
            if (options.StartupMode == AssetStartupMode.Strict)
            {
                ThreadRegistry.StopMainThread();
                throw new InvalidOperationException(
                    "Asset startup failed in Strict mode; the engine was stopped. See the log for details.",
                    failure
                );
            }

            Log.Warning(
                $"Asset startup failed in BestEffort mode; the engine continues. {failure.Message}"
            );
        }

        return new EngineApp(mainLoop, options.Frames);
    }

    // The project root is the folder that contains the fixed content folder. Prefer the build output
    // (the content is copied there), then the working directory, then the output directory itself.
    private static string ResolveProjectRoot()
    {
        string assetsFolder = AssetConstants.AssetsFolderName;
        string baseDirectory = AppContext.BaseDirectory;
        if (Directory.Exists(Path.Combine(baseDirectory, assetsFolder)))
            return baseDirectory;

        string workingDirectory = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(workingDirectory, assetsFolder)))
            return workingDirectory;

        return baseDirectory;
    }
}

/// <summary>One running engine: wait for the configured frame count, then dispose to stop every thread.</summary>
public sealed class EngineApp : IDisposable
{
    private readonly MainLoop _mainLoop;
    private readonly ulong _frames;
    private bool _disposed;

    internal EngineApp(MainLoop mainLoop, ulong frames)
    {
        _mainLoop = mainLoop ?? throw new ArgumentNullException(nameof(mainLoop));
        _frames = frames;
    }

    /// <summary>Waits until the main loop has processed <see cref="EngineOptions.Frames"/> frames or stopped.</summary>
    public void Run()
    {
        if (_frames == 0)
            return;

        while (Time.FrameIndex < _frames && _mainLoop.IsAlive)
        {
            SpinWait.SpinUntil(() => Time.FrameIndex >= _frames || !_mainLoop.IsAlive, 10);
        }

        if (Time.FrameIndex >= _frames)
        {
            Log.Info($"Reached frame {Time.FrameIndex}.");
        }
        else
        {
            Log.Error($"Main loop stopped after frame {Time.FrameIndex} (wanted {_frames}).");
        }
    }

    /// <summary>Stops the main loop (and with it the render and asset threads) and flushes the log.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ThreadRegistry.StopMainThread();
        Log.Info("All stopped.");

        try
        {
            Log.Flush();
        }
        catch (Exception)
        {
            // Logging is best effort: a failing flush must not change shutdown.
        }
    }
}