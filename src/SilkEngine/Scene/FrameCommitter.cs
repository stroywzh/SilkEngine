using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Threading;

namespace SilkEngine.Scene;

/// <summary>
/// 帧末提交编排（原 EngineLoop.CommitFrame 职责，A.4 拆分）：销毁处理 → 注册应用 → 快照 swap →
/// 排空 FrameCommit 阶段（Worker 结果发布）→ 资产完成拾取。
/// 依赖 Scene 提交机制（FrameSnapshotManager.CommitPending 契约），与 EngineLoop 同为编排层。
/// </summary>
internal sealed class FrameCommitter
{
    /// <summary>执行帧末提交（顺序契约：销毁 → 注册 → 快照 swap → 排空 FrameCommit → 资产完成）。</summary>
    /// <param name="snapshotManager">快照管理器（CommitPending 执行销毁/注册/swap）</param>
    /// <param name="registry">组件注册表</param>
    /// <param name="sceneManager">场景管理器（销毁队列与活动场景来源）</param>
    /// <param name="assetManager">资产管理器（帧末 ProcessCompleted 拾取）</param>
    /// <param name="runtime">线程运行时（FrameCommit 阶段排空；只传入阶段排空能力）</param>
    public void Commit(
        FrameSnapshotManager snapshotManager,
        ComponentRegistry registry,
        SceneManager sceneManager,
        AssetManager assetManager,
        ThreadRuntime runtime
    )
    {
        snapshotManager.CommitPending(
            registry,
            sceneManager._destroyQueue,
            sceneManager.ActiveScene,
            Time.DeltaTime
        );
        runtime.Drain(MainThreadPhase.FrameCommit);
        assetManager.ProcessCompleted();
    }
}
