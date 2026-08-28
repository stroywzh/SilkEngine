using SilkEngine;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

// 本类验证阶段 4 任务 1 后的注册路径：AddComponent 不再经 Services 回退，
// 而是经场景上下文（SceneManager.Create → Scene.CreateGameObject）携带的注册表解析。
[Collection("SceneManager")]
public class SceneManagerAttachTests : IDisposable
{
    /// <summary>测试级清理：注销测试内注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    private class Tracker : MonoBehaviour
    {
        public bool Destroy;
        public override void OnDestroy() => Destroy = true;
    }

    private static SceneManager NewAttached(out ComponentRegistry reg, out FrameSnapshotManager mgr)
    {
        reg = new ComponentRegistry();
        mgr = new FrameSnapshotManager();
        var sm = new SceneManager();
        sm.Attach(reg, mgr);
        return sm;
    }

    [Fact]
    public void CreateGameObject_RegistersIntoSceneContextRegistry()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var scene = sm.Create("T");
            var c = scene.CreateGameObject("C").AddComponent<Tracker>();
            reg.ApplyPending();

            Assert.Same(reg, scene.Context.Registry);
            Assert.Same(c, Assert.Single(reg.GetOfType<Tracker>()));
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    [Fact]
    public void AddComponent_ExplicitRegistry_OverridesContextRegistry()
    {
        var sm = NewAttached(out var contextReg, out _);
        var explicitReg = new ComponentRegistry();
        try
        {
            var scene = sm.Create("T");
            var go = scene.CreateGameObject("C");
            go.AddComponent<Tracker>(explicitReg);
            contextReg.ApplyPending();
            explicitReg.ApplyPending();

            Assert.Single(explicitReg.GetOfType<Tracker>());
            Assert.Empty(contextReg.GetOfType<Tracker>());
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    // 无上下文、无显式注册表：AddComponent 静默不注册（不再有 Services 回退链）
    [Fact]
    public void AddComponent_NoContextOrRegistry_SilentlySkipsRegistration()
    {
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();

        Assert.NotNull(c);
        Assert.Same(go, c.GameObject);
    }

    [Fact]
    public void AddObjectToScene_RegistersIntoAttachedRegistry()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var scene = new Scene("T");
            sm.LoadScene(scene);
            var go = new GameObject();
            go.AddComponent<Tracker>();
            reg.ApplyPending();
            Assert.Empty(reg.GetOfType<Tracker>()); // 无上下文 → 未注册

            Assert.True(sm.AddObjectToScene(go));
            Assert.False(sm.AddObjectToScene(go));   // 重复 → false
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>()); // Register 去重，无重复
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    [Fact]
    public void LoadScene_SingleArg_UsesAttachedRegistry()
    {
        var sm = NewAttached(out var reg, out var mgr);
        try
        {
            var s1 = new Scene("A");
            var go1 = new GameObject();
            var c1 = go1.AddComponent<Tracker>();
            s1.AddRootObject(go1);
            sm.LoadScene(s1);
            reg.ApplyPending();

            var s2 = new Scene("B");
            var go2 = new GameObject();
            var c2 = go2.AddComponent<Tracker>();
            s2.AddRootObject(go2);
            sm.LoadScene(s2);
            mgr.CommitPending(reg, sm._destroyQueue, s2, 0f); // 旧场景帧末统一销毁（架构 #6）

            Assert.True(c1.Destroy);                  // 旧场景组件收到 OnDestroy
            Assert.Same(c2, Assert.Single(reg.GetOfType<Tracker>())); // 仅新场景在册
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    [Fact]
    public void AddComponent_OnSceneCreatedObject_AutoRegisters()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var scene = sm.Create("T");
            scene.CreateGameObject("C").AddComponent<Tracker>();
            reg.ApplyPending();

            Assert.Single(reg.GetOfType<Tracker>());
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }
}
