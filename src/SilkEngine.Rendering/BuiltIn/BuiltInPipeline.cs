using SilkEngine.Asset;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Assets;

namespace SilkEngine.Rendering.BuiltIn;

// Prototype target ids: an implementation detail of the built-in pipeline, not a public contract.
internal static class BuiltInRenderTargets
{
    // Used directly as the GL framebuffer name, so 0 is the default framebuffer.
    internal const uint Window = 0;
}

/// <summary>
/// Prototype pipeline: draws the loaded textures as a Contain-fitted carousel over the animated clear
/// color, keeps the debug triangle on top, and falls back to the triangle when no texture is ready.
/// </summary>
public sealed class BuiltInPipeline : RenderPipelineBase
{
    // PPT-style timing: 3 s display and a 0.5 s cross-fade per slide; adjust here for the demo.
    private const double SlideSeconds = 3.0;
    private const double FadeSeconds = 0.5;

    private double _elapsedSeconds;
    private double _carouselSeconds;
    private int _width;
    private int _height;
    private bool _fullViewport;
    private DebugTriangle.Resources _triangle;
    private bool _triangleReady;
    private TexturedQuad.Resources _quad;
    private bool _quadReady;
    private bool _quadUsesConstants;
    private ConstantArena? _constants;

    // Render-thread owner of uploaded bindings; created in OnInitializeCore and released with the pipeline.
    private AssetRuntimeCache _runtime = null!;

    public override string Name => "Built-In Pipeline";

    protected override void OnInitializeCore()
    {
        IRenderDevice device =
            Backend.Device
            ?? throw new InvalidOperationException($"{Name}: the backend has no device.");

        _triangle = DebugTriangle.Create(device);
        _triangleReady = true;

        _runtime = new AssetRuntimeCache();
        _runtime.AddUploader(new TextureUploader());

        if (device is IRenderDeviceV2 && device.Capabilities.SupportsConstantBuffers)
        {
            _constants = new ConstantArena(device);
            _quad = TexturedQuad.Create(device);
            _quadUsesConstants = true;
        }
        else
        {
            _quad = TexturedQuad.CreateLegacy(device);
            _quadUsesConstants = false;
        }

        _quadReady = true;
    }

    protected override void OnDisposeCore()
    {
        if (Backend.Device is not { } device)
            return;

        _runtime?.Dispose(device);

        if (_constants is not null)
        {
            _constants.Dispose();
            _constants = null;
        }

        if (_quadReady)
        {
            _quadReady = false;
            TexturedQuad.Destroy(device, in _quad);
        }

        if (_triangleReady)
        {
            _triangleReady = false;
            DebugTriangle.Destroy(device, in _triangle);
        }
    }

    protected override void OnResize(int width, int height)
    {
        _width = width;
        _height = height;
        _fullViewport = false; // Size changed; the previous full-screen viewport no longer matches the framebuffer.

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"DEBUG pipeline resize -> {width}x{height}");
        }
    }

    protected override void RecordCommands(ICommandBuffer commands, FrameContext context)
    {
        // One shared clock for every DEBUG consumer, advanced once per recorded frame.
        _elapsedSeconds = (_elapsedSeconds + Time.DeltaSeconds) % (EngineMath.PI * 2);
        _carouselSeconds += Time.DeltaSeconds;

        IRenderDevice device =
            Backend.Device
            ?? throw new InvalidOperationException($"{Name}: the backend has no device.");

        // Released first, then ready, then upload: an id released and made ready in the same frame cannot linger.
        _runtime.Drain(device);

        if (_runtime.Order.Count == 0)
        {
            RecordDebugViewport(commands);
            RecordDebugClearColor(commands);
            RecordWhiteTriangle(commands);
            return;
        }

        EnsureFullViewport(commands);
        RecordDebugClearColor(commands);

        if (_quadUsesConstants && _constants is not null)
        {
            _constants.BeginFrame(context.FrameIndex);
            RecordCarousel(commands);
            _constants.Upload();
        }
        else
        {
            RecordSingleTexture(commands);
        }

        RecordDebugViewport(commands);
        RecordWhiteTriangle(commands);
    }

    private void RecordCarousel(ICommandBuffer commands)
    {
        int count = _runtime.Order.Count;
        TexturedQuad.BindState(commands, in _quad);

        if (count == 1)
        {
            RecordSlide(commands, 0, 1f);
            return;
        }

        double period = SlideSeconds + FadeSeconds;
        double time = _carouselSeconds % (period * count);
        int index = (int)(time / period);
        double local = time - index * period;

        if (local < FadeSeconds)
        {
            float fade = (float)(local / FadeSeconds);
            RecordSlide(commands, (index + count - 1) % count, 1f - fade);
            RecordSlide(commands, index, fade);
            return;
        }

        RecordSlide(commands, index, 1f);
    }

    private void RecordSlide(ICommandBuffer commands, int index, float alpha)
    {
        if (_constants is null || index < 0 || index >= _runtime.Order.Count)
            return;

        AssetId assetId = _runtime.Order[index];
        if (!_runtime.TryGet<TextureBinding>(assetId, out TextureBinding binding))
            return;

        DrawConstants2D constants = BuildContainConstants(
            binding.Texture.Width,
            binding.Texture.Height,
            alpha
        );
        uint offset = _constants.Write(in constants);
        TexturedQuad.RecordDraw(commands, binding.Handle, _constants.Buffer, offset);
    }

    private void RecordSingleTexture(ICommandBuffer commands)
    {
        if (!_quadReady || _runtime.Order.Count == 0)
            return;

        AssetId assetId = _runtime.Order[0];
        if (!_runtime.TryGet<TextureBinding>(assetId, out TextureBinding binding))
            return;

        TexturedQuad.RecordLegacy(commands, in _quad, binding.Handle);
    }

    private DrawConstants2D BuildContainConstants(int texWidth, int texHeight, float alpha)
    {
        if (_width <= 0 || _height <= 0 || texWidth <= 0 || texHeight <= 0)
            return new DrawConstants2D(1f, 1f, 0f, 0f, alpha, 1f, 1f, 1f);

        float viewAspect = (float)_width / _height;
        float textureAspect = (float)texWidth / texHeight;
        float halfX = MathF.Min(1f, textureAspect / viewAspect);
        float halfY = MathF.Min(1f, viewAspect / textureAspect);
        return new DrawConstants2D(halfX, halfY, 0f, 0f, alpha, 1f, 1f, 1f);
    }

    private void RecordWhiteTriangle(ICommandBuffer commands)
    {
        if (!_triangleReady)
            return;

        DebugTriangle.Record(commands, in _triangle);
    }

    private void EnsureFullViewport(ICommandBuffer commands)
    {
        if (_fullViewport || _width <= 0 || _height <= 0)
            return;

        var command = new SetViewportCommand(0, 0, _width, _height);
        commands.Record(in command);
        _fullViewport = true;
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
        _fullViewport = false;
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
}
