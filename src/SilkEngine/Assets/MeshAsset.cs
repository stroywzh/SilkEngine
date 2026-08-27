namespace SilkEngine.Assets;

/// <summary>网格资产载荷：CPU 侧顶点/布局/索引数据容器（GL 网格由渲染侧编译创建）</summary>
/// <param name="name">网格名称</param>
/// <param name="vertices">顶点数据（按 Layout 分量顺序排列的连续 float 数组）</param>
/// <param name="layout">顶点属性布局（每属性分量数；stride = 各分量数之和）</param>
/// <param name="indices">索引数据（null 表示非索引绘制）</param>
public sealed class MeshAsset(string name, float[] vertices, int[] layout, int[]? indices) : IAssetPayload
{
    /// <summary>网格名称</summary>
    public string Name { get; } = name;

    /// <summary>顶点数据（按 Layout 分量顺序排列的连续 float 数组）</summary>
    public float[] Vertices { get; } = vertices;

    /// <summary>顶点属性布局（每属性分量数）</summary>
    public int[] Layout { get; } = layout;

    /// <summary>索引数据（null 表示非索引绘制）</summary>
    public int[]? Indices { get; } = indices;
}
