using SilkEngine;
using SilkEngine.InputSystem;
using SilkEngine.Math;

namespace SandBox;

public class DEBUG_scripts : MonoBehaviour
{
    public override void OnAwake()
    {
        Log.Info("Hello World!");
    }

    public override void OnUpdate(float deltaTime)
    {
        if (Transform.Position.Y < 10)
        {
            if (Input.GetKey(KeyCode.W))
            {
                this.Transform.LocalPosition += Vector3.Up;
                Log.Info(Transform.Position);
            }
        }
        else
            Transform.LocalPosition = Vector3.Zero;
    }
}
