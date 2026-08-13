using SilkEngine.Math;
using SilkEngine.Scene.Serialization;

namespace SilkEngine.Scene;

/// <summary>相机组件：序列化参数由源生成器生成（字段名即 .scene 键）；ViewMatrix/ProjectionMatrix 为运行时计算，不参与序列化。</summary>
[SerializableInternal]
public partial class Camera : Component
{
    public float FieldOfView = 60f;
    public float NearClipPlane = 0.1f;
    public float FarClipPlane = 1000f;
    public float OrthographicSize = 5f;
    public bool Orthographic = false;

    public Matrix4x4 ViewMatrix { get; private set; }
    public Matrix4x4 ProjectionMatrix { get; private set; }

    public void UpdateMatrices(float aspectRatio)
    {
        Transform t = Transform;
        ViewMatrix = Matrix4x4.CreateLookAt(t.Position, t.Position + t.Forward, Vector3.Up);
        if (Orthographic)
        {
            float h = OrthographicSize;
            float w = h * aspectRatio;
            ProjectionMatrix = Matrix4x4.CreateOrthographic(w, h, NearClipPlane, FarClipPlane);
        }
        else
        {
            ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                FieldOfView * MathF.PI / 180f, aspectRatio, NearClipPlane, FarClipPlane);
        }
    }
}
