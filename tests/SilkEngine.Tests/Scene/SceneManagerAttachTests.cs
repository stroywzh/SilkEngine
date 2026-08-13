using SilkEngine;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

// 本类写入全局 Services 注册表（Shutdown/Register/Unregister），须与其余 Services 写入者
// （ServicesTests、AssetsFixture 相关类）同集合串行，避免跨集合互相清空
[Collection("Assets")]
public class SceneManagerAttachTests
{
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
        Services.Shutdown();
        var sm = NewAttached(out var reg, out _);
        Services.Register(sm);
        try
        {
            var c = new GameObject().AddComponent<Tracker>();
            reg.ApplyPending();
            Assert.Same(c, Assert.Single(reg.GetOfType<Tracker>()));
        }
        finally
        {
            Services.Shutdown();
        }
    }

    [Fact]
    public void AddComponent_NoRegistry_EquivalentToExplicitRegistry()
    {
        Services.Shutdown();
        var sm = NewAttached(out var reg, out _);
        Services.Register(sm);
        try
        {
            var viaFallback = new GameObject().AddComponent<Tracker>();
            var viaExplicit = new GameObject().AddComponent<Tracker>(reg);
            reg.ApplyPending();
            var all = reg.GetOfType<Tracker>();
            Assert.Equal(2, all.Count);              // 同注册表、同结果
            Assert.Contains(viaFallback, all);
            Assert.Contains(viaExplicit, all);
        }
        finally
        {
            Services.Shutdown();
        }
    }

    // 协调裁决 C1：回退链用 TryGet 静默不注册（保留旧测试语义）——无 Services 时不抛异常
    [Fact]
    public void AddComponent_NoServicesRegistered_SilentlySkipsRegistration()
    {
        Services.Shutdown();
        try
        {
            var go = new GameObject();
            var c = go.AddComponent<Tracker>();
            Assert.NotNull(c);
            Assert.Same(go, c.GameObject);
        }
        finally
        {
            Services.Shutdown();
        }
    }

    [Fact]
    public void AddObjectToScene_RegistersIntoAttachedRegistry()
    {
        Services.Shutdown();
        var sm = NewAttached(out var reg, out _);
        Services.Register(sm);
        try
        {
            var scene = new Scene("T");
            sm.LoadScene(scene);
            var go = new GameObject();
            go.AddComponent<Tracker>();
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>());

            Assert.True(sm.AddObjectToScene(go));
            Assert.False(sm.AddObjectToScene(go));   // 重复 → false
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>()); // Register 去重，无重复
        }
        finally
        {
            Services.Shutdown();
        }
    }

    [Fact]
    public void LoadScene_SingleArg_UsesAttachedRegistry()
    {
        Services.Shutdown();
        var sm = NewAttached(out var reg, out _);
        Services.Register(sm);
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

            Assert.True(c1.Destroy);                  // 旧场景组件收到 OnDestroy
            Assert.Same(c2, Assert.Single(reg.GetOfType<Tracker>())); // 仅新场景在册
        }
        finally
        {
            Services.Shutdown();
        }
    }

    // 迁移自 GameObjectTests.AddComponent_AmbientRegistry_AutoRegisters（ActiveRegistry 已删除，
    // ambient 注册表改为 Services 注入的 SceneManager.Registry）
    [Fact]
    public void AddComponent_AmbientRegistry_AutoRegisters()
    {
        Services.Shutdown();
        var sm = NewAttached(out var reg, out _);
        Services.Register(sm);
        try
        {
            var go = new GameObject();
            var c = go.AddComponent<Tracker>();
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<Tracker>());
            Assert.Same(c, reg.GetOfType<Tracker>()[0]);
        }
        finally
        {
            Services.Shutdown();
        }
    }
}
