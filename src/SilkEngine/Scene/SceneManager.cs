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

    internal bool _fristUpdateFlag;
    private bool _fristUpdateDone;

    internal static List<DestroyEntry> _destroyQueue = new();

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
        foreach (var go in scene._rootObjects)
            InvokeRecursive(go, c => (c as MonoBehaviour)?.OnAwake());
        _fristUpdateFlag = true;
        _fristUpdateDone = false;
    }

    public void LoadScene(Scene scene, ComponentRegistry? registry = null)
    {
        ActiveScene = scene;
        if (registry != null)
        {
            foreach (var go in scene._rootObjects)
                InvokeRecursive(go, c =>
                {
                    registry.Register(c);
                    (c as MonoBehaviour)?.OnAwake();
                });
            registry.ApplyPending();
        }
        else
        {
            foreach (var go in scene._rootObjects)
                InvokeRecursive(go, c => (c as MonoBehaviour)?.OnAwake());
        }
        _fristUpdateFlag = true;
        _fristUpdateDone = false;
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

    public void Tick(FrameSnapshot snapshot, ComponentRegistry registry, float dt)
    {
        if (_fristUpdateFlag && !_fristUpdateDone)
        {
            foreach (var mb in GetActiveMBs(registry))
                mb.OnStart();
            _fristUpdateDone = true;
        }

        foreach (var mb in GetActiveMBs(registry))
            mb.OnUpdate(dt);
    }

    public void FixedTick(FrameSnapshot snapshot, ComponentRegistry registry, float fdt)
    {
        foreach (var mb in GetActiveMBs(registry))
            mb.OnFixedUpdate(fdt);
    }

    public void LateTick(FrameSnapshot snapshot, ComponentRegistry registry)
    {
        foreach (var mb in GetActiveMBs(registry))
            mb.OnLateUpdate();
    }

    public void PostRender(FrameSnapshot snapshot, ComponentRegistry registry)
    {
        foreach (var mb in GetActiveMBs(registry))
            mb.OnPostRender();
    }

    private static IEnumerable<MonoBehaviour> GetActiveMBs(ComponentRegistry registry)
    {
        foreach (var mb in registry.GetOfType<MonoBehaviour>())
            if (mb.GameObject.IsActive && mb.Enabled)
                yield return mb;
    }
}
