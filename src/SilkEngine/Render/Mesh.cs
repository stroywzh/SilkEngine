using System.Linq;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render;

/// <summary>
/// 纯数据网格容器（顶点/布局/索引），后端将其编译为 GPU 网格 (IMesh)
/// </summary>
public class Mesh : IAsset
{
    /// <summary>网格标识名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>顶点数据（按 Layout 分量顺序排列的连续 float 数组）</summary>
    public float[] Vertices { get; init; } = [];

    /// <summary>顶点属性布局（每属性分量数；stride = 各分量数之和）</summary>
    public int[] Layout { get; init; } = [];

    /// <summary>索引数据（null 表示非索引绘制）</summary>
    public int[]? Indices { get; init; }

    /// <summary>顶点数（依 Layout 计算；Layout 为空时为 0）</summary>
    internal int VertexCount => Layout.Length > 0 ? Vertices.Length / Sum(Layout) : 0;

    private int? _hash;

    public override int GetHashCode()
    {
        if (_hash == null)
        {
            var h = new HashCode();
            h.Add(Name);
            foreach (var v in Vertices)
                h.Add(v);
            foreach (var l in Layout)
                h.Add(l);
            if (Indices != null)
                foreach (var i in Indices)
                    h.Add(i);
            _hash = h.ToHashCode();
        }
        return _hash.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Mesh m)
            return false;

        return Name == m.Name
            && Vertices.SequenceEqual(m.Vertices)
            && Layout.SequenceEqual(m.Layout)
            && (
                (Indices == null && m.Indices == null)
                || (Indices != null && m.Indices != null && Indices.SequenceEqual(m.Indices))
            );
    }

    private static int Sum(int[] arr)
    {
        int s = 0;
        foreach (var v in arr)
            s += v;
        return s;
    }
}
