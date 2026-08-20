using System.Collections.Generic;
using System.Linq;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Render;

public sealed class RenderBatch
{
    public IReadOnlyList<MeshRenderer> Renderers { get; init; } = [];
}

public sealed class RenderCollector
{
    private Camera? _defaultCamera;

    public void Gather(FrameSnapshot snapshot, out Camera camera, out List<RenderBatch> batches)
    {
        batches = [];

        camera = snapshot.GetComponents<Camera>()
            .FirstOrDefault(c => c.GameObject.IsActiveInHierarchy)
            ?? GetDefaultCamera();

        var renderers = snapshot.GetComponents<MeshRenderer>()
            .Where(r => r.Enabled && r.GameObject.IsActiveInHierarchy)
            .ToList();

        if (renderers.Count > 0)
        {
            batches.Add(new RenderBatch { Renderers = renderers });
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
