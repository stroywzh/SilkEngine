using ProjectEngine;
using ProjectEngine.Math;
using ProjectEngine.Render;
using ProjectEngine.Render.OpenGL;

var backend = new OpenGLRenderBackend();
var pipeline = new ForwardRenderPipeline();
var engine = new EngineLoop(backend, pipeline);

engine.Run();



// ======================== 测试方法 ========================

//测试移动
class DEBUG_SCRIPTS : MonoBehaviour
{
    public override void OnAwake()
    {
        Transform.LocalPosition = Vector3.Zero;
        Console.WriteLine("DEBUG_SCTIPT OnAwake");
    }

    Vector3 vector = new Vector3(0, 1, 0);

    public override void OnFixedTick(float deltaTime)
    {
        if (Transform.Position.Y < 10)
            Transform.LocalPosition += vector;
        else
            Transform.LocalPosition = Vector3.Zero;
        Console.WriteLine(this.Transform.Position);
    }
}
