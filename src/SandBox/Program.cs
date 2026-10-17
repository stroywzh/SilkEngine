using SilkEngine;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Rendering.BuiltIn.Backend;
using SilkEngine.Runtime;



Log.Info("HelloWorld!");
Application.RenderBackend = new SilkOpenGLBackend();
Application.RenderPipeline = new BuiltInPipeline();
Application.GraphicsDevice = new DEBUG_GraphicsDevice();
ThreadFactory.BuildMainThread();
Log.Info("Build结束");

Thread.Sleep(10000);
RuntimeRegistry.StopMainThread();

Log.Info("MainThreadStoped");


public class DEBUG_GraphicsDevice : IGraphicsDevice
{
    public int DEBUG = 1;
}