// using Silk.NET.Input;
using System.Runtime.InteropServices;
using SandBox;
using SilkEngine;
using SilkEngine.Asset;
// using SilkEngine.Asset.Import;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Runtime;

const string AssetsFolder = "Assets";

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

//TODO :这个应该由AssetThread负责，每次启动时扫描Root，也许可以引入SQLite，建立一个Library/，里面放ManagedAssets用于映射各种虚拟路径
//也作为实际变化检查
string? assetRoot = ResolveAssetDirectory(AssetsFolder);
string[] assetPaths = assetRoot is null ? Array.Empty<string>() : ScanPngAssets(assetRoot);
PrintBanner(args, assetRoot, assetPaths.Length, consoleState, logFile);

if (assetPaths.Length == 0)
{
    Log.Error(
        $"No PNG assets found under '{AssetsFolder}' (checked the working directory and the output directory)."
    );
    FlushLog();
    return 1;
}
// Application.AssetLoaders = 

// foreach (string assetPath in assetPaths)
// {
//     try
//     {
//         AssetImportResult imported = TextureImporter.Import(assetPath);
//         Log.Info($"Imported '{imported.SidecarPath}' (changed: {imported.Changed}).");
//     }
//     catch (Exception error)
//     {
//         // An import failure must not block startup: the render side falls back to the DEBUG triangle.
//         Report(error);
//     }
// }

Application.RenderBackend = new SilkOpenGLBackend();
Application.RenderPipeline = new BuiltInPipeline();

// Submitted before the engine starts: the requests only queue, and the asset worker consumes them once it runs.
var pendingLoads = new List<Task<Texture>>();
foreach (string assetPath in assetPaths)
{
    try
    {
        pendingLoads.Add(Assets.LoadAsync<Texture>(assetPath));
    }
    catch (Exception error)
    {
        Report(error);
    }
}

ulong? frameLimit = ParseFrameLimit(args);
var main = ThreadFactory.StartMainThread();

int loaded = 0;

// Completions are pumped by the main loop, so this only reports tasks that already finished.
void ReportCompleted()
{
    for (int i = pendingLoads.Count - 1; i >= 0; i--)
    {
        Task<Texture> load = pendingLoads[i];
        if (!load.IsCompleted)
            continue;

        pendingLoads.RemoveAt(i);

        if (load.IsCompletedSuccessfully)
        {
            Texture texture = load.Result;
            loaded++;
            Log.Info(
                $"Loaded texture {texture.Width}x{texture.Height} (assetId: {texture.AssetId.Value}) from a queued request."
            );
        }
        else if (load.IsCanceled)
        {
            Log.Warning("A queued texture load was cancelled.");
        }
        else
        {
            Report(load.Exception!.GetBaseException());
        }
    }
}

// IInputContext _input;
// bool shouldClose = false;
try
{
    // Input glue is not wired yet: GLFW input must be created on the window/render thread.
    // SpinWait.SpinUntil(() => main.State == EngineThreadState.Running);
    // if (Application.RenderBackend is SilkOpenGLBackend skb)
    // {
    //     // Running is entered before Initialize, so the window may still be null here.
    //     SpinWait.SpinUntil(() => skb.Inited);
    //     // TODO: GLFW input must be created on the window/render thread; this entry-thread path
    //     // currently throws GlfwException (gamepad) and makes DoEvents NRE.
    //     _input = skb.ForRegisterInput()!.CreateInput();

    //     for (int i = 0; i < _input.Keyboards.Count; i++)
    //     {
    //         _input.Keyboards[i].KeyDown += OnKeyDown;
    //         // _input.Keyboards[i].KeyUp += OnKeyUp;
    //     }
    // }

    // Entry thread waits without driving GL. The render owner pumps the prototype window.

    // The entry thread only reports queued loads; the engine thread pumps their completions.
    if (frameLimit is { } frames)
    {
        Log.Info($"Stopping after {frames} frames.");
        while (Time.FrameIndex < frames && main.IsAlive)
        {
            ReportCompleted();
            SpinWait.SpinUntil(() => Time.FrameIndex >= frames || !main.IsAlive, 1);
        }
    }
    else
    {
        while (main.IsAlive)
        {
            ReportCompleted();
            Thread.Sleep(1);
        }
    }

    ReportCompleted();
    Log.Info($"Loaded {loaded}/{assetPaths.Length} texture(s).");
}
finally
{
    ThreadRegistry.StopMainThread();
    Log.Info("All stopped.");
    if (logFile is not null)
        Log.Info($"log file: {logFile}");
    FlushLog();
}

return 0;

static string? ResolveAssetDirectory(string relativePath)
{
    string fromWorkingDirectory = Path.GetFullPath(relativePath);
    if (Directory.Exists(fromWorkingDirectory))
        return fromWorkingDirectory;

    string fromBaseDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, relativePath)
    );
    if (Directory.Exists(fromBaseDirectory))
        return fromBaseDirectory;

    return null;
}

static string[] ScanPngAssets(string root) =>
    Directory
        .GetFiles(root, "*.png", SearchOption.AllDirectories)
        .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal)
        .ToArray();

static void PrintBanner(
    string[] args,
    string? assetsRoot,
    int assetCount,
    string consoleState,
    string? logFile
)
{
    string exe = Environment.ProcessPath ?? "<unknown>";
    string arguments = args.Length == 0 ? "<none>" : string.Join(' ', args);

    // Banner lines are log records too, so the console state stays diagnosable in the log file when
    // the console itself is not visible.
    Log.Info("================ SilkEngine SandBox ================");
    Log.Info($"exe     : {exe}");
    Log.Info($"cwd     : {Environment.CurrentDirectory}");
    Log.Info($"args    : {arguments}");
    Log.Info($"assets  : {assetsRoot ?? "<not found: Assets>"}");
    Log.Info($"png     : {assetCount}");
    Log.Info($"console : {consoleState}");
    Log.Info($"logFile : {logFile ?? "<disabled>"}");
    Log.Info(
        $"runtime : {RuntimeInformation.FrameworkDescription} / {RuntimeInformation.OSDescription}"
    );
    Log.Info("====================================================");
}

static void Report(Exception error) => Log.Exception(error);

static void FlushLog()
{
    try
    {
        Log.Flush();
    }
    catch (Exception)
    {
        // Logging is best effort: a failing flush must not change the exit code.
    }
}

static ulong? ParseFrameLimit(string[] args)
{
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

    return null;
}
