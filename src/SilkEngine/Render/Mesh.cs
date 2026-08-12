using System.Linq;

namespace SilkEngine.Render;

public class Mesh
{
    public string Name { get; init; } = "";
    public float[] Vertices { get; init; } = [];
    public int[] Layout { get; init; } = [];
    public int[]? Indices { get; init; }
    public int VertexCount => Layout.Length > 0 ? Vertices.Length / Sum(Layout) : 0;

    private int? _hash;

    public override int GetHashCode()
    {
        if (_hash == null)
        {
            var h = new HashCode();
            h.Add(Name);
            foreach (var v in Vertices) h.Add(v);
            foreach (var l in Layout) h.Add(l);
            if (Indices != null) foreach (var i in Indices) h.Add(i);
            _hash = h.ToHashCode();
        }
        return _hash.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Mesh m) return false;
        return Name == m.Name
            && Vertices.SequenceEqual(m.Vertices)
            && Layout.SequenceEqual(m.Layout)
            && ((Indices == null && m.Indices == null)
                || (Indices != null && m.Indices != null && Indices.SequenceEqual(m.Indices)));
    }

    private static int Sum(int[] arr)
    {
        int s = 0;
        foreach (var v in arr)
            s += v;
        return s;
    }
}
