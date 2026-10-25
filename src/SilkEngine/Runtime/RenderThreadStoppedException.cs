namespace SilkEngine.Runtime;

/// <summary>Thrown when the render thread stopped before the requested operation completed.</summary>
/// <remarks>
/// C-12/C-10: it keeps <see cref="System.OperationCanceledException"/> semantics (callers already catch
/// cancellation), so it is not an <see cref="EngineException"/>. It lives in <c>SilkEngine.Runtime</c>
/// next to <see cref="EngineThreadStateException"/>.
/// </remarks>
internal sealed class RenderThreadStoppedException : System.OperationCanceledException
{
    internal RenderThreadStoppedException()
        : base("Render thread stopped before the requested operation completed.") { }
}
