using ProjectEngine;
using ProjectEngine.Math;
using ProjectEngine.Render;
using ProjectEngine.Render.OpenGL;

var backend = new OpenGLRenderBackend();
var engine = new EngineLoop(backend);

engine.Initialize().Run();



// ======================== 测试方法 ========================

//测试移动
class DEBUG_SCRIPTS : MonoBehaviour
{
    public override void OnAwake()
    {
        Transform.LocalPosition = Vector3.Zero;
        Log.Info("DEBUG_SCRIPT: Awake");
    }

    Vector3 vector = new Vector3(0, 1, 0);

    public override void OnFixedTick(float deltaTime)
    {
        if (Transform.Position.Y < 10)
            Transform.LocalPosition += vector;
        else
            Transform.LocalPosition = Vector3.Zero;
        Log.Info(this.Transform.Position);
    }
}
