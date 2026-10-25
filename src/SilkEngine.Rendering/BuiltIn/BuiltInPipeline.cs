using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>
/// Prototype target registration shared by the built-in pipeline and the GL device.
/// </summary>
/// <remarks>
/// Internal : the ids are an implementation detail of the built-in pipeline, not a
/// public contract. Typed handles carry <c>uint Id</c>.
/// </remarks>
internal static class BuiltInRenderTargets
{
    /// <summary>
    /// Prototype window target. It is used directly as the GL framebuffer name, so 0 is the
    /// default framebuffer.
    /// </summary>
    internal const uint Window = 0;
}

/// <summary>
/// Prototype pipeline skeleton. Recording is faked here until Program supplies its own commands.
/// </summary>
public sealed class BuiltInPipeline : RenderPipelineBase
{
    private double _elapsedSeconds;
    private int _width;
    private int _height;
    private BufferHandle _triangleBuffer;
    private bool _triangleReady;

    public override string Name => "Built-In Pipeline";

    //<intetrhoic/>
    protected override void OnInitializeCore()
    {
        var device =
            Backend.Device
            ?? throw new InvalidOperationException($"{Name}: the backend has no device.");

        _triangleBuffer = DebugTriangle.Create(device);
        _triangleReady = true;
    }

    //<intetrhoic/>
    protected override void OnDisposeCore()
    {
        if (!_triangleReady)
            return;

        _triangleReady = false;
        Backend.Device?.DestroyBuffer(_triangleBuffer);
    }

    //<intetrhoic/>
    protected override void OnResize(int width, int height)
    {
        _width = width;
        _height = height;

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"DEBUG pipeline resize -> {width}x{height}");
        }
    }

    protected override void RecordCommands(ICommandBuffer commands, FrameContext context)
    {
        // One shared clock for every DEBUG consumer, advanced once per recorded frame.
        _elapsedSeconds = (_elapsedSeconds + Time.DeltaSeconds) % (EngineMath.PI * 2);

        RecordDebugViewport(commands);
        RecordDebugClearColor(commands);
        RecordDebugTriangle(commands);
    }

    //DEBUG_Only
    private void RecordDebugViewport(ICommandBuffer commands)
    {
        if (_width <= 0 || _height <= 0)
            return;

        var scale = 0.5 + 0.5 * EngineMath.Sin((float)_elapsedSeconds); // 0.5 .. 1.0, always positive
        var viewWidth = EngineMath.Max(1, (int)(_width * scale));
        var viewHeight = EngineMath.Max(1, (int)(_height * scale));
        var x = (_width - viewWidth) / 2;
        var y = (_height - viewHeight) / 2;

        var command = new SetViewportCommand(x, y, viewWidth, viewHeight);
        commands.Record(in command);
    }

    //DEBUG_Only
    private void RecordDebugClearColor(ICommandBuffer commands)
    {
        float t = (float)_elapsedSeconds;
        var color = new Color(
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t))),
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t + 2))),
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t + 4))),
            255
        );

        var command = new ClearColorCommand(
            new RenderTargetHandle(BuiltInRenderTargets.Window),
            color
        );
        commands.Record(in command);
    }

    //DEBUG_Only
    private void RecordDebugTriangle(ICommandBuffer commands)
    {
        if (!_triangleReady)
            return;

        DebugTriangle.Record(commands, _triangleBuffer);
    }
}
