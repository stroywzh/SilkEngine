namespace SilkEngine.Assets;

/// <summary>网格资产载荷：CPU 侧顶点/布局/索引数据容器（GL 网格由渲染侧编译创建）；数组在构造时复制，发布后不可变</summary>
public sealed class MeshAsset : IAssetPayload
{
    /// <summary>创建网格资产；顶点/布局/索引数组在构造时复制为私有副本。</summary>
    /// <param name="name">网格名称</param>
    /// <param name="vertices">顶点数据（按 Layout 分量顺序排列的连续 float 数组）</param>
    /// <param name="layout">顶点属性布局（每属性分量数；stride = 各分量数之和）</param>
    /// <param name="indices">索引数据（null 表示非索引绘制）</param>
    /// <exception cref="ArgumentNullException">vertices 或 layout 为 null</exception>
    public MeshAsset(string name, float[] vertices, int[] layout, int[]? indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(layout);
        Name = name;
        Vertices = vertices.ToArray();
        Layout = layout.ToArray();
        Indices = indices?.ToArray();
    }

    /// <summary>网格名称</summary>
    public string Name { get; }

    /// <summary>顶点数据（按 Layout 分量顺序排列的连续 float 数组）</summary>
    public float[] Vertices { get; }

    /// <summary>顶点属性布局（每属性分量数）</summary>
    public int[] Layout { get; }

    /// <summary>索引数据（null 表示非索引绘制）</summary>
    public int[]? Indices { get; }
}
