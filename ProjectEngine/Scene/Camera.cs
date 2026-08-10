using ProjectEngine.Math;

namespace ProjectEngine;

public class Camera : Component
{
    public float FieldOfView { get; set; } = 60f;
    public float NearClipPlane { get; set; } = 0.1f;
    public float FarClipPlane { get; set; } = 1000f;
    public float OrthographicSize { get; set; } = 5f;
    public bool Orthographic { get; set; } = true;
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
