using System.Collections.Generic;
using System.Linq;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Scene;

/// <summary>
/// 场景渲染器收集提供者：从当前帧快照收集活跃且启用的 <see cref="MeshRenderer"/>
/// （宿主组合根注册进 RenderCollector；新增渲染器类型经 provider 接入，不修改 EngineLoop）。
/// </summary>
public sealed class SceneRendererProvider(FrameSnapshotManager snapshotManager) : IRendererProvider
{
    /// <inheritdoc />
    public IEnumerable<IRenderable> Collect()
        => snapshotManager.Current.GetComponents<MeshRenderer>()
            .Where(r => r.Enabled && r.GameObject.IsActiveInHierarchy);
}
