using ProjectEngine.Render;
using ProjectEngine.Render.OpenGL;

namespace ProjectEngine;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello World!\n");
        var backend = new OpenGLRenderBackend();
        var pipeline = new ForwardRenderPipeline();
        EngineLoop engine = new(backend,pipeline);
        Console.WriteLine("SetUp Finished");
        engine.Run();
    }
}
