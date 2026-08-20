using SilkEngine;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

// 本类测试依赖 ctor 自注册的 SceneManager 处于 Services 注册态（AddComponent 回退链解析），
// 注册窗口必须与全部 SceneManager 创建者/ambient 使用者串行——故与其余 SceneManager 测试同集合；
// 测试级 Dispose 注销（Unregister 幂等），不再 Shutdown 全局注册表（避免并行清空其他集合）
[Collection("SceneManager")]
public class SceneManagerAttachTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
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
    public void AddComponent_NoRegistry_FallsBackToAttachedRegistry()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var c = new GameObject().AddComponent<Tracker>();
            Services.Unregister<SceneManager>(); // 回退解析完成即注销，窗口缩至瞬时（防并行集合注册冲突）
            reg.ApplyPending();
            Assert.Same(c, Assert.Single(reg.GetOfType<Tracker>()));
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    [Fact]
    public void AddComponent_NoRegistry_EquivalentToExplicitRegistry()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var viaFallback = new GameObject().AddComponent<Tracker>();
            var viaExplicit = new GameObject().AddComponent<Tracker>(reg);
            Services.Unregister<SceneManager>(); // 回退解析完成即注销，窗口缩至瞬时（防并行集合注册冲突）
            reg.ApplyPending();
            var all = reg.GetOfType<Tracker>();
            Assert.Equal(2, all.Count);              // 同注册表、同结果
            Assert.Contains(viaFallback, all);
            Assert.Contains(viaExplicit, all);
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }

    // 协调裁决 C1：回退链用 TryGet 静默不注册（保留旧测试语义）——无 Services 时不抛异常
    [Fact]
    public void AddComponent_NoServicesRegistered_SilentlySkipsRegistration()
    {
        try
        {
            var go = new GameObject();
            var c = go.AddComponent<Tracker>();
            Assert.NotNull(c);
            Assert.Same(go, c.GameObject);
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
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
            Services.Unregister<SceneManager>(); // 回退解析完成即注销，窗口缩至瞬时（防并行集合注册冲突）
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>());

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
            Services.Unregister<SceneManager>(); // 回退解析完成即注销，窗口缩至瞬时（防并行集合注册冲突）
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

    // 迁移自 GameObjectTests.AddComponent_AmbientRegistry_AutoRegisters（ActiveRegistry 已删除，
    // ambient 注册表改为 Services 注入的 SceneManager.Registry）
    [Fact]
    public void AddComponent_AmbientRegistry_AutoRegisters()
    {
        var sm = NewAttached(out var reg, out _);
        try
        {
            var go = new GameObject();
            var c = go.AddComponent<Tracker>();
            Services.Unregister<SceneManager>(); // 回退解析完成即注销，窗口缩至瞬时（防并行集合注册冲突）
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>());
            Assert.Same(c, reg.GetOfType<Tracker>()[0]);
        }
        finally
        {
            Services.Unregister<SceneManager>();
        }
    }
}
