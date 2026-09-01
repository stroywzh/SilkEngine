using System.Collections.Generic;
using System.Linq;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Scene;

/// <summary>
/// 场景渲染世界：Main 域从当前帧快照一次性构建只读 <see cref="RenderSourceSnapshot"/>
/// （活跃相机含默认相机回退；渲染器经已注册 <see cref="IRendererProvider"/> 收集）。
/// EngineLoop 只消费快照，不查询具体场景类型（新增渲染器经 provider 接入）。
/// </summary>
public sealed class SceneRenderWorld
{
    private readonly FrameSnapshotManager _snapshotManager;
    private readonly IReadOnlyList<IRendererProvider> _providers;
    private Camera? _defaultCamera;

    /// <summary>创建场景渲染世界。</summary>
    /// <param name="snapshotManager">帧快照管理器（相机/渲染器来源）</param>
    /// <param name="providers">渲染器收集提供者列表（可为空）</param>
    public SceneRenderWorld(FrameSnapshotManager snapshotManager, IEnumerable<IRendererProvider> providers)
    {
        _snapshotManager = snapshotManager;
        _providers = [.. providers];
    }

    /// <summary>构建本帧渲染源快照：活跃相机（空时默认相机回退）+ 全部 provider 渲染器。</summary>
    /// <returns>只读渲染源快照</returns>
    public RenderSourceSnapshot BuildSnapshot()
    {
        var frame = _snapshotManager.Current;
        var cameras = frame.GetComponents<Camera>().Where(c => c.GameObject.IsActiveInHierarchy).ToList();
        if (cameras.Count == 0)
            cameras.Add(_defaultCamera ??= new GameObject("Default Camera").AddComponent<Camera>());

        var renderers = new List<IRenderable>();
        foreach (var provider in _providers)
            foreach (var renderable in provider.Collect())
                renderers.Add(renderable);
        return new RenderSourceSnapshot(cameras, renderers);
    }
}
