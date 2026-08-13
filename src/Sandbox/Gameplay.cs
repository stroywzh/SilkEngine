using System;
using SilkEngine;
using SilkEngine.InputSystem;
using SilkEngine.Math;

namespace SandBox;

public class PlayerController : MonoBehaviour
{
    public float Speed = 5f;
    public CameraFollow? Camera;

    public override void OnUpdate(float dt)
    {
        var dir = Vector3.Zero;
        if (Input.GetKey(KeyCode.W))
            dir += Vector3.Forward;
        if (Input.GetKey(KeyCode.S))
            dir -= Vector3.Forward;
        if (Input.GetKey(KeyCode.A))
            dir -= Vector3.Right;
        if (Input.GetKey(KeyCode.D))
            dir += Vector3.Right;

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

        // if (!Input.Mouse.MiddleButton)
        //     return;

        var mouse = Input.Mouse.MoveVector;
        _yaw += mouse.X * Sensitivity;
        _pitch = Math.Clamp(_pitch - mouse.Y * Sensitivity, -1.2f, 1.2f);

        float x = MathF.Cos(_pitch) * MathF.Sin(_yaw) * Distance;
        float y = MathF.Sin(_pitch) * Distance;
        float z = MathF.Cos(_pitch) * MathF.Cos(_yaw) * Distance;

        Transform.LocalPosition = Target.Transform.Position + new Vector3(x, y, z);
        Transform.LocalRotation = Quaternion.Euler(_pitch, _yaw, 0);
    }
}
