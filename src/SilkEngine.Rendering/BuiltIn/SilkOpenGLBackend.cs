using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>Silk.NET window/context factory and CPU command consumer, driven by one render thread.</summary>
/// <remarks>
/// Window, GL context and presentation are owned by the device created in <see cref="CreateDevice"/>
/// (C-06); the backend keeps only the factory and the command queue. Window events are pumped through
/// the device, which is why this type no longer implements <see cref="IRenderWindowHost"/>.
/// </remarks>
public sealed class SilkOpenGLBackend : RenderBackendBase
{
    /// <inheritdoc />
    public override string Name => "Silk.NET OpenGL";

    // REN-N-01 override template (kept as a comment, not code): an implementation assembly that
    // declares [RenderCommand] structs gets a populated RenderCommandTable; before the first command it
    // gets an empty one, so this override always compiles:
    //
    //   protected override bool Dispatch(byte id, ReadOnlySpan<byte> payload, ref CommandContext ctx) =>
    //       RenderCommandTable.TryDispatch(id, payload, ref ctx) || base.Dispatch(id, payload, ref ctx);
    //
    // Recording uses the generated assembly-local RenderCommandRecorder.Record(buffer, in command)
    // (not an extension method, which would collide with the engine's extensions across assemblies).

    /// <summary>Creates the window, its GL context and the device that owns both.</summary>
    /// <returns>The created device; the backend disposes it.</returns>
    /// <exception cref="InvalidOperationException">The window has no GL context.</exception>
    protected override IRenderDevice CreateDevice()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.FramesPerSecond = 0;
        options.ShouldSwapAutomatically = false;
        options.VSync = true;

        var window = Window.Create(options);
        try
        {
            window.Initialize();
            window.GLContext!.MakeCurrent();
            var gl = window.CreateOpenGL();
            try
            {
                return new OpenGLDevice(window, gl);
            }
            catch
            {
                gl.Dispose();
                throw;
            }
        }
        catch
        {
            window.Dispose();
            throw;
        }
    }
}
