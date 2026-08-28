using System;
using SilkEngine.Scene;
using SilkEngine.InputSystem;
using SilkEngine.Math;

namespace SandBox;

/// <summary>游戏动作声明（业务唯一输入入口；经 Input 门面转发到 EngineHost 装配的动作服务）。</summary>
public static class GameplayActions
{
    public const string Map = "Gameplay";

    /// <summary>注册游戏动作映射（启动时调用一次）。</summary>
    public static void Configure()
    {
        Input.AddActionMap(Map, map =>
        {
            map.Button("Jump", KeyCode.Space);
            map.Axis("MoveX", KeyCode.A, KeyCode.D);
            map.Axis("MoveZ", KeyCode.S, KeyCode.W);
            map.MouseDelta("Look", 0.002f);
        });
    }
}

public class PlayerController : MonoBehaviour
{
    public float Speed = 5f;
    public CameraFollow? Camera;

    public override void OnUpdate(float dt)
    {
        var dir = Vector3.Zero;
        dir += Input.GetAxis(GameplayActions.Map, "MoveZ") * Vector3.Forward;
        dir += Input.GetAxis(GameplayActions.Map, "MoveX") * Vector3.Right;

        if (dir == Vector3.Zero)
            return;

        if (Camera != null)
            dir = Quaternion.Euler(0, Camera.Yaw, 0) * dir;
        Transform.LocalPosition += dir.Normalized * (Speed * dt);
    }
}

public class CameraFollow : MonoBehaviour
{
    public GameObject? Target;
    public float Distance = 8f;
    public float Sensitivity = 0.002f;
    private float _yaw;
    private float _pitch = 0.3f;

    public float Yaw => _yaw;

    public override void OnLateUpdate()
    {
        if (Target == null)
            return;

        var mouse = Input.GetMouseDelta(GameplayActions.Map, "Look");
        _yaw += mouse.X;
        _pitch = Math.Clamp(_pitch - mouse.Y, -1.2f, 1.2f);

        float x = MathF.Cos(_pitch) * MathF.Sin(_yaw) * Distance;
        float y = MathF.Sin(_pitch) * Distance;
        float z = MathF.Cos(_pitch) * MathF.Cos(_yaw) * Distance;

        Transform.LocalPosition = Target.Transform.Position + new Vector3(x, y, z);
        Transform.LocalRotation = Quaternion.Euler(_pitch, _yaw, 0);
    }
}
