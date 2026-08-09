namespace ProjectEngine.Render;

public class Mesh
{
    public string Name { get; init; } = "";
    public float[] Vertices { get; init; } = [];
    public int[] Layout { get; init; } = [];
    public int VertexCount => Layout.Length > 0 ? Vertices.Length / Sum(Layout) : 0;

    private static int Sum(int[] arr)
    {
        int s = 0;
        foreach (var v in arr)
            s += v;
        return s;
    }
}
