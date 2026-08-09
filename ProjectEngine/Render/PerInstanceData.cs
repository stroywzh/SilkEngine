namespace ProjectEngine.Render;

/// <summary>
/// Blittable 结构体
/// <br/>包含 GPU 实例化用的 4×4 行主序矩阵。每个实例一条，上传至实例缓冲区
/// </summary>
public struct PerInstanceData
{
    /// <summary>矩阵第 0 行</summary>
    public float M00,
        M01,
        M02,
        M03;

    /// <summary>矩阵第 1 行</summary>
    public float M10,
        M11,
        M12,
        M13;

    /// <summary>矩阵第 2 行</summary>
    public float M20,
        M21,
        M22,
        M23;

    /// <summary>矩阵第 3 行</summary>
    public float M30,
        M31,
        M32,
        M33;
}
