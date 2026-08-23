using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Scene;

/// <summary>相机组件：ViewMatrix/ProjectionMatrix 为运行时计算。</summary>
public class Camera : Component, ICameraView
{
    /// <summary>透视视场角（度）。</summary>
    public float FieldOfView = 60f;

    /// <summary>近裁剪面距离（>0）。</summary>
    public float NearClipPlane = 0.1f;

    /// <summary>远裁剪面距离（&gt; NearClipPlane）。</summary>
    public float FarClipPlane = 1000f;

    /// <summary>正交视口半高（Orthographic=true 时生效）。</summary>
    public float OrthographicSize = 5f;

    /// <summary>true = 正交投影，false = 透视投影。</summary>
    public bool Orthographic = false;

    /// <summary>世界空间视图矩阵（UpdateMatrices 计算）。</summary>
    public Matrix4x4 ViewMatrix { get; private set; }

    /// <summary>投影矩阵（UpdateMatrices 计算）。</summary>
    public Matrix4x4 ProjectionMatrix { get; private set; }

    /// <summary>
    /// 按当前 Transform 与宽高比重算视图/投影矩阵（正交与透视按 Orthographic 切换）。
    /// </summary>
    /// <param name="aspectRatio">视口宽高比（宽/高）</param>
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
                Mathf.Deg2Rad * FieldOfView, aspectRatio, NearClipPlane, FarClipPlane);
        }
    }
}
