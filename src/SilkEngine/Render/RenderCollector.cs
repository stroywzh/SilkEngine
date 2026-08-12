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
    private Camera? _defaultCamera;

    public void Gather(FrameSnapshot snapshot, out Camera camera, out List<RenderBatch> batches)
    {
        batches = [];

        camera = snapshot.GetComponents<Camera>()
            .FirstOrDefault(c => c.GameObject.IsActive)
            ?? GetDefaultCamera();

        var renderers = snapshot.GetComponents<MeshRenderer>()
            .Where(r => r.Enabled && r.GameObject.IsActive)
            .ToList();

        if (renderers.Count > 0)
        {
            batches.Add(new RenderBatch { Camera = camera, Renderers = renderers });
        }
    }

    private Camera GetDefaultCamera()
    {
        if (_defaultCamera != null)
            return _defaultCamera;
        var host = new GameObject("Default Camera");
        _defaultCamera = host.AddComponent<Camera>();
        return _defaultCamera;
    }
}
