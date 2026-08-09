using System;

namespace ProjectEngine.Render;

/// <summary>
/// Blittable 结构体
/// <br/>表示未来 C++ 互操作的渲染命令。现在不使用
/// </summary>
public struct RenderPacket
{
    /// <summary>
    /// 网格 GPU 资源句柄
    /// </summary>
    public IntPtr MeshHandle;

    /// <summary>
    /// 材质 GPU 资源句柄
    /// </summary>
    public IntPtr MaterialHandle;

    /// <summary>
    /// 渲染层
    /// </summary>
    public int Layer;

    /// <summary>
    /// 实例缓冲区中的偏移量
    /// </summary>
    public int InstanceOffset;

    /// <summary>
    /// 实例数量
    /// </summary>
    public int InstanceCount;
}
