using SilkEngine.Asset;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Rendering.Abstraction;

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

    // The render thread is the only creator/destroyer: AssetId is asset identity (read-only snapshot); TextureHandle is a device resource handle.
    private readonly Dictionary<AssetId, TextureHandle> _textures = new();
    private readonly List<AssetId> _textureOrder = new();
    private readonly List<AssetId> _pending = new();

    public override string Name => "Built-In Pipeline";

    protected override void OnInitializeCore()
    {
        IRenderDevice device =
            Backend.Device
            ?? throw new InvalidOperationException($"{Name}: the backend has no device.");

        _triangle = DebugTriangle.Create(device);
        _triangleReady = true;

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

        foreach (TextureHandle texture in _textures.Values)
            device.DestroyTexture(texture);

        _textures.Clear();
        _textureOrder.Clear();
        _pending.Clear();

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

        // Collect ready before releasing: an id released then made ready in the same frame is cleared from pending here and cannot linger.
        DrainReady();
        DrainReleased(device);
        UploadPending(device);

        if (_textureOrder.Count == 0)
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
        int count = _textureOrder.Count;
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
        if (_constants is null || index < 0 || index >= _textureOrder.Count)
            return;

        AssetId assetId = _textureOrder[index];
        if (!Assets.TryGet<Texture>(assetId, out Texture? texture) || texture is null)
            return;

        DrawConstants2D constants = BuildContainConstants(texture.Width, texture.Height, alpha);
        uint offset = _constants.Write(in constants);
        TexturedQuad.RecordDraw(commands, _textures[assetId], _constants.Buffer, offset);
    }

    private void RecordSingleTexture(ICommandBuffer commands)
    {
        if (!_quadReady || _textureOrder.Count == 0)
            return;

        AssetId assetId = _textureOrder[0];
        TexturedQuad.RecordLegacy(commands, in _quad, _textures[assetId]);
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

    private void DrainReleased(IRenderDevice device)
    {
        while (Assets.TryTakeReleased(out AssetId released))
        {
            _pending.Remove(released);

            if (!_textures.Remove(released, out TextureHandle texture))
                continue;

            _textureOrder.Remove(released);
            device.DestroyTexture(texture);
        }
    }

    private void DrainReady()
    {
        while (Assets.TryTakeReady(out AssetId ready))
            _pending.Add(ready);
    }

    // No catch: keep pending on lookup/upload failure and retry next frame; render-thread per-frame recovery logs exceptions.
    private void UploadPending(IRenderDevice device)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            AssetId assetId = _pending[i];

            if (!Assets.TryGet<Texture>(assetId, out Texture? texture) || texture is null)
            {
                // Missing from both snapshots = unregistered and never reused; drop it instead of retrying forever.
                if (!AssetTable.TryGet(assetId, out _))
                    _pending.RemoveAt(i);

                continue;
            }

            if (!AssetTable.TryGet(assetId, out RawDataContainer? payload) || payload is null)
                continue;
            if (_textures.ContainsKey(assetId))
            {
                _pending.RemoveAt(i);
                continue;
            }

            TextureHandle uploaded = device.CreateTexture(
                texture.Width,
                texture.Height,
                payload.Serialized.Span
            );

            _textures.Add(assetId, uploaded);
            _textureOrder.Add(assetId);
            _pending.RemoveAt(i);

            if (Application.LogSetting.EnableRenderLog)
            {
                Log.Debug(
                    $"Uploaded texture {texture.Width}x{texture.Height} for asset {assetId.Value}."
                );
            }
        }
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
