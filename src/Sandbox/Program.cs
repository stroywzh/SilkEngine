using ProjectEngine;
using ProjectEngine.Render;
using ProjectEngine.Render.OpenGL;

var backend = new OpenGLRenderBackend();
var pipeline = new ForwardRenderPipeline();
var engine = new EngineLoop(backend, pipeline);
Console.WriteLine("SetUp Finished");
engine.Run();
