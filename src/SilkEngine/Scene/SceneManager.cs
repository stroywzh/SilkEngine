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

    internal static List<DestroyEntry> _destroyQueue = new();

    internal static ComponentRegistry? ActiveRegistry { get; set; }

    public static readonly SceneManager Instance = new();

    public SceneManager()
    {
        Object.DestroyHandler += (obj, delay) =>
            _destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay });
    }

    public static Scene? ActiveScene { get; internal set; }

    public void LoadScene(Scene scene)
    {
        ActiveScene = scene;
    }

    public void LoadScene(Scene scene, ComponentRegistry? registry = null)
    {
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

    private void InvokeRecursive(GameObject go, Action<Component> action)
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

    /// <summary>
    /// 这个东西很麻烦，涉及到后续对于Scripting API等的设计
    /// </summary>
    /// <param name="obj"></param>
    public static void AddObjectToScene(Object obj)
    {
        if (obj is MonoBehaviour mb)
        {
            ActiveScene?.AddRootObject(mb.GameObject);
            foreach (var c in mb.GameObject._components)
            {
                c.OnEnable();
            }

            mb.OnAwake();
        }
    }
}
