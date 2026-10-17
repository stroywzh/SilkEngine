using SilkEngine;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Rendering.BuiltIn.Backend;
using SilkEngine.Runtime;

Application.RenderBackend = new SilkOpenGLBackend();
Application.RenderPipeline = new BuiltInPipeline();
Application.GraphicsDevice = new PrototypeGraphicsDevice();
var main = ThreadFactory.BuildMainThread();
try
{
    // Entry thread waits without driving GL. The render owner pumps the prototype window.
    SpinWait.SpinUntil(() => !main.IsAlive);
}
finally
{
    RuntimeRegistry.StopMainThread();
}

public sealed class PrototypeGraphicsDevice : IGraphicsDevice { }
