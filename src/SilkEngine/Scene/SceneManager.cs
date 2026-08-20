using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SilkEngine.Core;
using SilkEngine.Scene.Serialization;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Scene;

public class SceneManager : IDisposable
{
    internal struct DestroyEntry
    {
        public Object Target;
        public float Delay;
        public Scene? Origin; // 帧末从原场景容器摘除（LoadScene 后 ActiveScene 已切换）
    }

    internal List<DestroyEntry> _destroyQueue = new();

    private ComponentRegistry? _registry;
    private FrameSnapshotManager? _snapshotManager;

    /// <summary>引擎注入：注册表与快照管理器（EngineLoop.Initialize 调用，替代原 ActiveRegistry）。</summary>
    internal void Attach(ComponentRegistry registry, FrameSnapshotManager snapshotManager)
    {
        _registry = registry;
        _snapshotManager = snapshotManager;
    }

    /// <summary>已注入的组件注册表（GameObject.AddComponent 回退链与派发消费）。</summary>
    internal ComponentRegistry? Registry => _registry;

    private readonly Action<Object, float> _destroyHandler;

    /// <summary>实例构造订阅全局销毁事件（引擎单实例；测试经 Dispose 解绑防累积）</summary>
    public SceneManager()
    {
        _destroyHandler = (obj, delay) =>
            _destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay, Origin = ActiveScene });
        Object.DestroyHandler += _destroyHandler;

        Services.Register(this);
    }

    /// <summary>解绑 DestroyHandler（Services.Shutdown 反序释放 / 测试夹具调用）</summary>
    public void Dispose() => Object.DestroyHandler -= _destroyHandler;

    public SilkEngine.Scene.Scene? ActiveScene { get; internal set; }

    public void LoadScene(SilkEngine.Scene.Scene scene) => LoadScene(scene, _registry);

    public void LoadScene(SilkEngine.Scene.Scene scene, ComponentRegistry? registry = null)
    {
        if (ActiveScene != null)
        {
            foreach (var go in ActiveScene._rootObjects.ToArray())
                Object.Destroy(go); // 统一队列 + 幂等 + 立即失活
        }
        ActiveScene = scene;
        if (registry != null)
        {
            foreach (var go in scene._rootObjects)
                InvokeRecursive(go, c => registry.Register(c));
            registry.ApplyPending();
        }
        if (LogConfig.Scene)
            Log.Info($"[Scene] Loaded '{scene.Name}' (roots: {scene.GetRootGameObjects().Length})");
    }

    internal void RegisterScene()
    {
        if (ActiveScene == null || _registry == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(go, c => _registry.Register(c));
        _registry.ApplyPending();
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

    private IEnumerable<MonoBehaviour> GetActiveMBs(FrameSnapshot snapshot)
    {
        foreach (var list in snapshot.MbGroups)
            foreach (var mb in list)
                if (mb.GameObject.IsActiveInHierarchy && mb.Enabled)
                    yield return mb;
    }

    /// <summary>运行时向活动场景添加 GameObject（含子树）；注册进已注入注册表，不重复触发生命周期。</summary>
    public bool AddObjectToScene(GameObject go)
    {
        if (
            ActiveScene == null
            || ActiveScene._rootObjects.Contains(go)
            || go.Transform.Parent != null
        )
        {
            if (LogConfig.Scene)
                Log.Info(
                    $"[Scene] AddObjectToScene failed for '{go.Name}' (already in scene / no active scene / has parent)"
                );
            return false;
        }
        ActiveScene.AddRootObject(go);
        InvokeRecursive(go, c => _registry?.Register(c));
        if (LogConfig.Scene)
            Log.Info($"[Scene] Added object '{go.Name}'");
        return true;
    }

    /// <summary>便捷重载：从组件定位其 GameObject。</summary>
    public bool AddObjectToScene(MonoBehaviour mb) => AddObjectToScene(mb.GameObject);

    /// <summary>
    /// 从 .scene JSON 文件加载场景：读文件 → SceneSerializer.Deserialize → LoadScene（带注入的注册表）。
    /// 返回是否成功；失败（文件缺失/无权限/JSON 格式错误）记录错误日志且不抛未捕获异常。
    /// </summary>
    public bool LoadSceneFromFile(string path)
    {
        try
        {
            var scene = SceneSerializer.Deserialize(File.ReadAllText(path));
            LoadScene(scene, _registry);
            if (LogConfig.Scene)
                Log.Info($"[Scene] Loaded scene from file '{path}'");
            return true;
        }
        catch (Exception e)
            when (e
                    is IOException
                        or UnauthorizedAccessException
                        or JsonException
                        or InvalidOperationException
            )
        {
            Log.Error($"LoadSceneFromFile failed: {path} — {e.Message}");
            return false;
        }
    }
}
