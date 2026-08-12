using System.Collections.Generic;
using System.Linq;

namespace SilkEngine.Render;

public sealed class RenderBatch
{
    public Camera Camera { get; init; } = null!;
    public IReadOnlyList<MeshRenderer> Renderers { get; init; } = [];
}

public sealed class RenderCollector
{
    public void Gather(FrameSnapshot snapshot, out Camera camera, out List<RenderBatch> batches)
    {
        batches = [];

        camera = snapshot.GetComponents<Camera>()
            .FirstOrDefault(c => c.GameObject.IsActive)
            ?? new Camera { Orthographic = true };

        var renderers = snapshot.GetComponents<MeshRenderer>()
            .Where(r => r.Enabled && r.GameObject.IsActive)
            .ToList();

        if (renderers.Count > 0)
        {
            batches.Add(new RenderBatch { Camera = camera, Renderers = renderers });
        }
    }
}
