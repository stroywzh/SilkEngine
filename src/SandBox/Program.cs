using SandBox;
using SilkEngine;
using SilkEngine.Asset;
using SilkEngine.Rendering.BuiltIn;

// Console first: everything below (banner, file log, engine output) depends on a usable console.
string consoleState = ConsoleSetup.Initialize();

string? logFile = null;
try
{
    // Console and file receive the same records: the console keeps the run visible, the file keeps
    // it durable.
    logFile = Log.EnableFileLog();
}
catch (Exception error)
{
    Log.Error($"File logging unavailable: {error.Message}");
}

Log.Info($"console: {consoleState}");

// The host owns content startup (scan/import/catalog) and every engine thread; Program only supplies
// the rendering configuration and asks for textures.
ulong frames = ParseFrames(args);
using var app = EngineHost.Start(
    new EngineOptions
    {
        Frames = frames,
        AssetBootstrap = ContentAssets.CreateBootstrap(),
        RenderBackend = new SilkOpenGLBackend(),
        RenderPipeline = new BuiltInPipeline(),
    }
);
Log.Info($"Will Stopping after {frames} frames.");

// Explicit content paths: startup already imported and cataloged them, so these are plain loads.
string[] texturePaths = ["Assets/Image.png", "Assets/hakiri.png"];
int loaded = 0;
foreach (string texturePath in texturePaths)
{
    try
    {
        Texture texture = await Assets.LoadAsync<Texture>(texturePath);
        loaded++;
        Log.Info(
            $"Loaded texture {texture.Width}x{texture.Height} (assetId: {texture.AssetId.Value})."
        );
    }
    catch (Exception error)
    {
        // A missing or invalid texture must not stop the engine: the pipeline falls back to the DEBUG triangle.
        Log.Exception(error);
    }
}

Log.Info($"Loaded {loaded}/{texturePaths.Length} texture(s).");

try
{
    app.Run();
}
finally
{
    if (logFile is not null)
        Log.Info($"log file: {logFile}");
}
Log.Info($"Stopped at:{Time.FrameIndex}");
return 0;

static ulong ParseFrames(string[] args)
{
    const ulong DefaultFrames = 50000;

    for (int i = 0; i < args.Length - 1; i++)
    {
        if (!string.Equals(args[i], "--frames", StringComparison.Ordinal))
            continue;

        if (!ulong.TryParse(args[i + 1], out ulong frames) || frames == 0)
            throw new ArgumentException(
                $"--frames requires a positive frame count, got '{args[i + 1]}'.",
                nameof(args)
            );

        return frames;
    }

    return DefaultFrames;
}