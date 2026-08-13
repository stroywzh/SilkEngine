using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SilkEngine.Core.Assets.Serialization;

namespace SilkEngine;

public class SceneManager : IDisposable
{
    internal struct DestroyEntry
    {
        public Object Target;
        public float Delay;
    }

    internal List<DestroyEntry> _destroyQueue = new();

    /// <summary>
    /// 过渡期保留（Part 4 由 Attach(ComponentRegistry, FrameSnapshotManager) 注入替代后移除；
    /// GameObject 回退链与 EngineLoop.Initialize 仍写此静态）
    /// </summary>
    internal static ComponentRegistry? ActiveRegistry { get; set; }

    private readonly Action<Object, float> _destroyHandler;

    /// <summary>实例构造订阅全局销毁事件（引擎单实例；测试经 Dispose 解绑防累积）</summary>
    public SceneManager()
    {
        _destroyHandler = (obj, delay) =>
            _destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay });
        Object.DestroyHandler += _destroyHandler;
    }

    /// <summary>解绑 DestroyHandler（Services.Shutdown 反序释放 / 测试夹具调用）</summary>
    public void Dispose() => Object.DestroyHandler -= _destroyHandler;

    public Scene? ActiveScene { get; internal set; }

    public void LoadScene(Scene scene) => LoadScene(scene, ActiveRegistry);

    public void LoadScene(Scene scene, ComponentRegistry? registry = null)
    {
        if (ActiveScene != null && registry != null)
        {
            foreach (var go in ActiveScene._rootObjects)
                InvokeRecursive(go, c =>
                {
                    registry.Unregister(c);
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
    public bool AddObjectToScene(GameObject go)
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
    public bool AddObjectToScene(MonoBehaviour mb) => AddObjectToScene(mb.GameObject);

    /// <summary>
    /// 从 .scene JSON 文件加载场景：读文件 → SceneSerializer.Deserialize → LoadScene（带 ActiveRegistry）。
    /// 返回是否成功；失败（文件缺失/无权限/JSON 格式错误）记录错误日志且不抛未捕获异常。
    /// </summary>
    public bool LoadSceneFromFile(string path)
    {
        try
        {
            var scene = SceneSerializer.Deserialize(File.ReadAllText(path));
            LoadScene(scene, ActiveRegistry);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Log.Error($"LoadSceneFromFile failed: {path} — {e.Message}");
            return false;
        }
    }
}
