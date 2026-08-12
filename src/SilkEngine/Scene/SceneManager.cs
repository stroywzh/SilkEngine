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
}
