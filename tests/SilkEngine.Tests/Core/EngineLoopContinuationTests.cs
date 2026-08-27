using System;
using System.IO;
using System.Linq;
using SilkEngine.Core;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// 引擎帧末 Continuation 接线锁：AssetOperation 的 await 恢复投递到 MainThreadPhase.Continuation，
/// EngineLoop 必须在 FrameCommit 之后排空该阶段，否则引擎内异步 LoadAsync 的续延永不执行。
/// </summary>
[Collection("SceneManager")]
public class EngineLoopContinuationTests : IDisposable
{
    private static readonly string SourceRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine"));

    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    private static string FindSource(string fileName)
    {
        var file = Directory.GetFiles(SourceRoot, fileName, SearchOption.AllDirectories).SingleOrDefault();
        return file ?? throw new FileNotFoundException($"{fileName} 未在 src/SilkEngine 下找到");
    }

    private static (SceneManager Sm, FrameSnapshotManager Mgr, ComponentRegistry Reg) SetupScene()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var sm = new SceneManager();
        Services.Unregister<SceneManager>(); // 消除注册窗口（本测试实例自足，无 ambient 依赖）
        sm.Attach(reg, mgr);
        var s = new Scene("T");
        s.AddRootObject(new GameObject());
        sm.LoadScene(s, reg);
        mgr.CommitPending(reg, sm._destroyQueue, s, 0f);
        return (sm, mgr, reg);
    }

    [Fact]
    public void EngineLoop_DrainsContinuationAfterFrameCommit()
    {
        var source = File.ReadAllText(FindSource("EngineLoop.cs"));
        var commit = source.IndexOf("_frameCommitter.Commit(", StringComparison.Ordinal);
        var drain = source.IndexOf("Drain(MainThreadPhase.Continuation)", StringComparison.Ordinal);

        Assert.True(commit >= 0, "EngineLoop.Run 必须调用 _frameCommitter.Commit");
        Assert.True(drain > commit, "EngineLoop 必须在 FrameCommit 排空之后接线 Continuation 阶段排空");
    }

    [Fact]
    public void FrameEnd_ContinuationRunsAfterCommitInEngineOrder()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var (sm, mgr, reg) = SetupScene();
        var order = new List<string>();
        var committer = new FrameCommitter();

        // 与 EngineLoop.Run 帧末相同序列：Commit（内部排空 FrameCommit）→ 之后排空 Continuation
        runtime.MainThread.Post(MainThreadPhase.FrameCommit, () => order.Add("frame-commit"));
        runtime.MainThread.Post(MainThreadPhase.Continuation, () => order.Add("continuation"));

        committer.Commit(mgr, reg, sm, runtime);
        runtime.Drain(MainThreadPhase.Continuation);

        Assert.Equal(["frame-commit", "continuation"], order);
    }

    [Fact]
    public void FrameEnd_ContinuationCallbackPostedFromWorker_ExecutesOnMain()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var (sm, mgr, reg) = SetupScene();
        var observedDomain = ThreadDomain.Unknown;
        var committer = new FrameCommitter();

        // 模拟 AssetOperation 在 Worker 域完成：续延经 MainThreadDispatcher.Post(Continuation) 投递；
        // 同步阻塞等待避免 await 跳到线程池导致 Main 域断言误判
        runtime.Background.Run(_ =>
        {
            runtime.MainThread.Post(MainThreadPhase.Continuation, () => observedDomain = runtime.CurrentDomain);
            return ValueTask.CompletedTask;
        }).AsTask().GetAwaiter().GetResult();

        committer.Commit(mgr, reg, sm, runtime);
        runtime.Drain(MainThreadPhase.Continuation);

        Assert.Equal(ThreadDomain.Main, observedDomain);
    }
}
