using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>Explicit prototype target registration shared by the built-in pipeline and GL RHI.</summary>
public static class BuiltInTargets
{
    /// <summary>
    /// Prototype window target. It is used directly as the GL framebuffer name, so 0 is the
    /// default framebuffer.
    /// </summary>
    public const ushort Window = 0;
}

/// <summary>
/// Prototype pipeline skeleton. Recording is faked here until Program supplies its own commands.
/// </summary>
public sealed class BuiltInPipeline : RenderPipelineBase
{
    private double _elapsedSeconds;
    private int _width;
    private int _height;

    public override string Name => "Built-In Pipeline";

    protected override void OnInitialize() { }

    /// <summary>Remembers the drawable size the render loop reported after a window resize.</summary>
    /// <remarks>The render loop calls this on the render thread as soon as the host size changes, so
    /// the next recorded frame already uses the new size.</remarks>
    protected override void OnResize(int width, int height)
    {
        _width = width;
        _height = height;

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"DEBUG pipeline resize -> {width}x{height}");
        }
    }

    protected override void RecordCommands(ICommandBuffer commands, RenderContext context)
    {
        // One shared clock for every DEBUG consumer, advanced once per recorded frame.
        _elapsedSeconds = (_elapsedSeconds + Time.UnscaledDeltaTime) % (EngineMath.PI * 2);

        DEBUG_SetViewport(commands);
        DEBUG_ClearColor(commands);
        DEBUG_DrawTriangle(commands);
    }

    /// <summary>
    /// DEBUG ONLY: records a viewport that pulses between 50% and 100% of the drawable, centred,
    /// driven by the same elapsed time as the clear color. Delete this method once Program supplies
    /// its own command content.
    /// </summary>
    /// <param name="commands">Writable buffer for the current frame.</param>
    public void DEBUG_SetViewport(ICommandBuffer commands)
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

    /// <summary>
    /// DEBUG ONLY: stands in for business-provided recording. Delete this method once Program
    /// supplies its own command content.
    /// </summary>
    /// <param name="commands">Writable buffer for the current frame.</param>
    /// <remarks>
    /// The target handle it passes is the "mapping data" business would provide later.
    /// </remarks>
    public void DEBUG_ClearColor(ICommandBuffer commands)
    {
        float t = (float)_elapsedSeconds;
        var color = new Color(
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t))),
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t + 2))),
            (byte)(255 * (0.5 + 0.5 * EngineMath.Sin(t + 4))),
            255
        );

        var command = new ClearColorCommand(new RenderTargetHandle(BuiltInTargets.Window), color);
        commands.Record(in command);
    }

    /// <summary>
    /// DEBUG ONLY: draws the RHI's built-in triangle. Because it runs after the pulsing viewport
    /// command, the triangle is the visible proof that the viewport animation reaches the device.
    /// Delete this method once Program supplies its own command content.
    /// </summary>
    /// <param name="commands">Writable buffer for the current frame.</param>
    public void DEBUG_DrawTriangle(ICommandBuffer commands)
    {
        var command = new DrawCommand(3, 0);
        commands.Record(in command);
    }
}
