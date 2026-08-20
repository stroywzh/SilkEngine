using System.Collections.Generic;
using System.Linq;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Render;

/// <summary>一批渲染对象：当前实现为单批（全部活跃 MeshRenderer）</summary>
public sealed class RenderBatch
{
    /// <summary>本批网格渲染器列表</summary>
    public IReadOnlyList<MeshRenderer> Renderers { get; init; } = [];
}

/// <summary>
/// 主线程渲染收集器：从帧快照选取活跃相机与可见渲染器，组装渲染批次
/// </summary>
public sealed class RenderCollector
{
    private Camera? _defaultCamera;

    /// <summary>
    /// 收集当前帧渲染数据：相机取首个层级活跃的 Camera（无则用内置默认相机），
    /// 渲染器取 Enabled 且层级活跃的 MeshRenderer
    /// </summary>
    /// <param name="snapshot">当前帧组件快照</param>
    /// <param name="camera">选中的相机（可能为内置默认相机）</param>
    /// <param name="batches">组装出的渲染批次列表</param>
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
