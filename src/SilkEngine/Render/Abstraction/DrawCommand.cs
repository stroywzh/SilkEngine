namespace SilkEngine.Render;

/// <summary>
/// 主线程传给渲染线程的绘制命令基类
/// <br/>子类支持单次绘制和 GPU 实例化。
/// </summary>
public abstract class DrawCommand
{
    /// <summary>
    /// 要使用的着色器
    /// </summary>
    public Shader? Shader { get; init; }

    /// <summary>
    /// 要渲染的网格
    /// </summary>
    public Mesh? Mesh { get; init; }

    /// <summary>
    /// 材质参数(uniform 值)
    /// </summary>
    public MaterialLegacy? Material { get; init; }

    /// <summary>
    /// 此绘制命令是否启用
    /// </summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// 绘制单个网格实例
/// </summary>
public sealed class SingleDrawCommand : DrawCommand
{
    /// <summary>模型矩阵（对象世界变换；与 View/Projection 一并上传 uModel/uView/uProjection/uMVP）</summary>
    public Math.Matrix4x4? ModelMatrix { get; init; }

    /// <summary>本命令的视图矩阵（渲染状态，非材质属性；每帧由相机 UpdateMatrices 计算）</summary>
    public Math.Matrix4x4? ViewMatrix { get; init; }

    /// <summary>本命令的投影矩阵（渲染状态，非材质属性；每帧由相机 UpdateMatrices 计算）</summary>
    public Math.Matrix4x4? ProjectionMatrix { get; init; }
}

/// <summary>
/// 在一次 GPU 调用中绘制多个网格实例
/// </summary>
public sealed class InstancedDrawCommand : DrawCommand
{
    /// <summary>
    /// 实例数量
    /// </summary>
    public int InstanceCount { get; init; }

    /// <summary>
    /// 逐实例数据(世界矩阵)，
    /// <br/>会上传至 GPU 实例缓冲区
    /// </summary>
    public PerInstanceData[] InstanceData { get; init; } = [];
}
