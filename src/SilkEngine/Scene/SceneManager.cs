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

    internal volatile bool fristUpdate = false;

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
        fristUpdate = true;
        _fristUpdateFlag = true;
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
    }

    internal void RegisterScene(ComponentRegistry registry)
    {
        if (ActiveScene == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(go, c => registry.Register(c));
        registry.ApplyPending();
    }

    public void Tick(float dt)
    {
        if (ActiveScene == null)
            return;

        if (fristUpdate)
        {
            foreach (var go in ActiveScene._rootObjects)
                InvokeRecursive(go, c => (c as MonoBehaviour)?.OnStart());
        }

        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnUpdate(dt);
                }
            );
    }

    public void FixedTick(float fdt)
    {
        if (ActiveScene == null)
            return;

        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnFixedUpdate(fdt);
                }
            );
    }

    public void LateTick()
    {
        if (ActiveScene == null)
            return;

        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnLateUpdate();
                }
            );
    }

    public void PostRender()
    {
        if (ActiveScene == null)
            return;

        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnPostRender();
                }
            );
    }

    public void ForEachComponent<T>(Action<T> action)
        where T : Component
    {
        if (ActiveScene == null)
            return;

        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is T t)
                        action(t);
                }
            );
    }

    public void ProcessDestroys(float dt)
    {
        for (int i = _destroyQueue.Count - 1; i >= 0; i--)
        {
            var e = _destroyQueue[i];
            e.Delay -= dt;
            if (e.Delay <= 0)
            {
                if (e.Target is MonoBehaviour mb)
                    mb.OnDestroy();
                if (e.Target is GameObject go)
                {
                    InvokeRecursive(go, c => (c as MonoBehaviour)?.OnDestroy());
                    if (ActiveScene != null)
                        ActiveScene._rootObjects.Remove(go);
                }
                _destroyQueue.RemoveAt(i);
            }
            else
                _destroyQueue[i] = e;
        }
    }

    private void InvokeRecursive(GameObject go, Action<Component> action)
    {
        foreach (var c in go._components)
            action(c);
        foreach (var child in go.Transform.Children)
            InvokeRecursive(child.GameObject!, action);
    }

    public void TickWithSnapshot(FrameSnapshot snapshot, ComponentRegistry registry, float dt)
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

    public void FixedTickWithSnapshot(FrameSnapshot snapshot, ComponentRegistry registry, float fdt)
    {
        foreach (var mb in GetActiveMBs(registry))
            mb.OnFixedUpdate(fdt);
    }

    public void LateTickWithSnapshot(FrameSnapshot snapshot, ComponentRegistry registry)
    {
        foreach (var mb in GetActiveMBs(registry))
            mb.OnLateUpdate();
    }

    public void PostRenderWithSnapshot(FrameSnapshot snapshot, ComponentRegistry registry)
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
