using SilkEngine.Core.Assets.Serialization;
using SilkEngine.Math;

namespace SilkEngine;

public class Camera : Component
{
    public float FieldOfView { get; set; } = 60f;
    public float NearClipPlane { get; set; } = 0.1f;
    public float FarClipPlane { get; set; } = 1000f;
    public float OrthographicSize { get; set; } = 5f;
    public bool Orthographic { get; set; } = false;
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

    /// <summary>反序列化：恢复相机参数（缺失字段保留默认值）。</summary>
    public override void ReadFrom(SerializedNode node)
    {
        FieldOfView = node.GetFloat("FieldOfView") is var f && f != 0f ? f : FieldOfView;
        NearClipPlane = node.GetFloat("NearClipPlane") is var n && n != 0f ? n : NearClipPlane;
        FarClipPlane = node.GetFloat("FarClipPlane") is var ff && ff != 0f ? ff : FarClipPlane;
        OrthographicSize = node.GetFloat("OrthographicSize") is var os && os != 0f ? os : OrthographicSize;
        Orthographic = node.GetBool("Orthographic");
    }

    /// <summary>序列化：写出全部相机参数。</summary>
    public override void WriteTo(SerializedNode node)
    {
        node.SetFloat("FieldOfView", FieldOfView);
        node.SetFloat("NearClipPlane", NearClipPlane);
        node.SetFloat("FarClipPlane", FarClipPlane);
        node.SetFloat("OrthographicSize", OrthographicSize);
        node.SetBool("Orthographic", Orthographic);
    }
}
