using System;
using System.Collections.Generic;

namespace ProjectEngine;

public static class SceneManager
{
    internal struct DestroyEntry
    {
        public Object Target;
        public float Delay;
    }

    internal static List<DestroyEntry> _destroyQueue = new();

    static SceneManager()
    {
        Object.DestroyHandler += (obj, delay) =>
            _destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay });
    }

    public static Scene? ActiveScene { get; internal set; }

    public static void LoadScene(Scene scene)
    {
        ActiveScene = scene;
        foreach (var go in scene._rootObjects)
            InvokeRecursive(go, c => (c as MonoBehaviour)?.OnAwake());
        foreach (var go in scene._rootObjects)
            InvokeRecursive(go, c => (c as MonoBehaviour)?.OnStart());
    }

    public static void Tick(float dt)
    {
        if (ActiveScene == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnTick(dt);
                }
            );
    }

    public static void FixedTick(float fdt)
    {
        if (ActiveScene == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnFixedTick(fdt);
                }
            );
    }

    public static void LateTick()
    {
        if (ActiveScene == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(
                go,
                c =>
                {
                    if (c is MonoBehaviour mb && mb.GameObject.IsActive && mb.Enabled)
                        mb.OnLateTick();
                }
            );
    }

    public static void PostRender()
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

    public static void ForEachComponent<T>(Action<T> action)
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

    public static void ProcessDestroys(float dt)
    {
        for (int i = _destroyQueue.Count - 1; i >= 0; i--)
        {
            var e = _destroyQueue[i];
            e.Delay -= dt;
            if (e.Delay <= 0)
            {
                if (e.Target is MonoBehaviour mb)
                    mb.OnDestroy();
                _destroyQueue.RemoveAt(i);
            }
            else
                _destroyQueue[i] = e;
        }
    }

    private static void InvokeRecursive(GameObject go, Action<Component> action)
    {
        foreach (var c in go._components)
            action(c);
        foreach (var child in go.Transform.Children)
            InvokeRecursive(child.GameObject!, action);
    }
}
