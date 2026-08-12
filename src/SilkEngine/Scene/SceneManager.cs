using System;
using System.Collections.Generic;

namespace SilkEngine;

public class SceneManager
{
    internal struct DestroyEntry
    {
        public Object Target;
        public float Delay;
    }

    internal List<DestroyEntry> _destroyQueue = new(); // 实例成员（原 static）

    internal static ComponentRegistry? ActiveRegistry { get; set; }

    public static readonly SceneManager Instance;

    static SceneManager()
    {
        Instance = new SceneManager();
        Object.DestroyHandler += (obj, delay) =>
            Instance._destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay });
    }

    public SceneManager() { } // 不再订阅

    public static Scene? ActiveScene { get; internal set; }

    public void LoadScene(Scene scene)
    {
        ActiveScene = scene;
    }

    public void LoadScene(Scene scene, ComponentRegistry? registry = null)
    {
        if (ActiveScene != null && registry != null)
        {
            foreach (var go in ActiveScene._rootObjects)
                InvokeRecursive(go, c =>
                {
                    registry.Unregister(c);
                    c._destroyed = true;
                    c.OnDestroy();
                });
        }
        ActiveScene = scene;
        if (registry != null)
        {
            foreach (var go in scene._rootObjects)
                InvokeRecursive(go, c => registry.Register(c));
            registry.ApplyPending();
        }
    }

    internal void RegisterScene(ComponentRegistry registry)
    {
        if (ActiveScene == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(go, c => registry.Register(c));
        registry.ApplyPending();
    }

    private static void InvokeRecursive(GameObject go, Action<Component> action)
    {
        foreach (var c in go._components)
            action(c);
        foreach (var child in go.Transform.Children)
            InvokeRecursive(child.GameObject!, action);
    }

    public void Tick(FrameSnapshot snapshot, float dt)
    {
        foreach (var mb in GetActiveMBs(snapshot))
        {
            if (!mb.Started)
            {
                mb.Started = true;
                mb.OnStart();
            }
            mb.OnUpdate(dt);
        }
    }

    public void FixedTick(FrameSnapshot snapshot, float fdt)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnFixedUpdate(fdt);
    }

    public void LateTick(FrameSnapshot snapshot)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnLateUpdate();
    }

    public void PostRender(FrameSnapshot snapshot)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnPostRender();
    }

    private static IEnumerable<MonoBehaviour> GetActiveMBs(FrameSnapshot snapshot)
    {
        foreach (var g in snapshot.Groups)
        {
            if (
                g.ComponentType != typeof(MonoBehaviour)
                && !g.ComponentType.IsSubclassOf(typeof(MonoBehaviour))
            )
                continue;
            foreach (var c in g.Components)
                if (c is MonoBehaviour mb && mb.GameObject.IsActiveInHierarchy && mb.Enabled)
                    yield return mb;
        }
    }

    /// <summary>运行时向活动场景添加 GameObject（含子树），仅注册；Awake/Enable 由组件工厂保证，不重复触发。</summary>
    public static bool AddObjectToScene(GameObject go)
    {
        if (ActiveScene == null
            || ActiveScene._rootObjects.Contains(go)
            || go.Transform.Parent != null)
            return false;
        ActiveScene.AddRootObject(go);
        var registry = ActiveRegistry;
        InvokeRecursive(go, c => registry?.Register(c));
        return true;
    }

    /// <summary>便捷重载：从组件定位其 GameObject。</summary>
    public static bool AddObjectToScene(MonoBehaviour mb) => AddObjectToScene(mb.GameObject);
}
