using System;

namespace SilkEngine.Render;

/// <summary>
/// GPU 网格缓冲区
/// <br/>由后端从 Mesh 数据创建
/// </summary>
public interface IMesh : IDisposable
{
    /// <summary>
    /// 绘制单个网格实例
    /// </summary>
    void Draw();

    /// <summary>
    /// 在一次 GPU 调用中绘制多个网格实例
    /// </summary>
    void DrawInstanced(int instanceCount);

    /// <summary>
    /// 网格顶点数量
    /// </summary>
    int VertexCount { get; }

    /// <summary>
    /// 是否支持 GPU 实例化
    /// </summary>
    bool SupportsInstancing { get; }
}
