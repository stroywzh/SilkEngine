using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Scene;

/// <summary>
/// 帧末提交编排：销毁处理 → 注册应用 → 快照 swap → 排空 FrameCommit 阶段（管线结果在
/// FrameCommit 内投递，AssetManager 经 ResultSink 应用缓存）。
/// 依赖 Scene 提交机制（FrameSnapshotManager.CommitPending 契约），与 EngineLoop 同为编排层。
/// </summary>
internal sealed class FrameCommitter
{
    /// <summary>执行帧末提交（顺序契约：销毁 → 注册 → 快照 swap → 排空 FrameCommit → 管线结果应用）。</summary>
    /// <param name="snapshotManager">快照管理器（CommitPending 执行销毁/注册/swap）</param>
    /// <param name="registry">组件注册表</param>
    /// <param name="sceneManager">场景管理器（销毁队列与活动场景来源）</param>
    /// <param name="runtime">线程运行时（FrameCommit 阶段排空；只传入阶段排空能力）</param>
    public void Commit(
        FrameSnapshotManager snapshotManager,
        ComponentRegistry registry,
        SceneManager sceneManager,
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
    }
}
