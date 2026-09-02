using System;
using System.Collections.Generic;
using SilkEngine.Assets;
using SilkEngine.Core;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Scene;

/// <summary>
/// 场景管理门面（引擎单实例，EngineLoop 创建并注册 Services）：
/// LoadScene 旧场景对象进 Destroy 队列、新场景组件立即注册（帧末 ApplyPending 生效）；
/// 帧末提交语义 —— 销毁（OnDestroy + Unregister）与快照 swap 统一由 FrameSnapshotManager.CommitPending 执行。
/// Tick/FixedTick/LateTick/PostRender 读取当前快照派发 MonoBehaviour 回调。
/// </summary>
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
    public void Attach(ComponentRegistry registry, FrameSnapshotManager snapshotManager)
    {
        _registry = registry;
        _snapshotManager = snapshotManager;
    }

    /// <summary>已注入的组件注册表（GameObject.AddComponent 回退链与派发消费）。</summary>
    internal ComponentRegistry? Registry => _registry;

    /// <summary>资产服务（EngineLoop 装配后注入；SceneContext 消费，无资产场景为 null）。</summary>
    public AssetManager? AssetService { get; set; }

    private readonly Action<Object, float> _destroyHandler;

    /// <summary>实例构造订阅全局销毁事件（引擎单实例；测试经 Dispose 解绑防累积）。构造不注册全局服务（Host 集中装配）。</summary>
    public SceneManager()
    {
        _destroyHandler = (obj, delay) =>
            _destroyQueue.Add(new DestroyEntry { Target = obj, Delay = delay, Origin = ActiveScene });
        Object.DestroyHandler += _destroyHandler;
    }

    /// <summary>解绑 DestroyHandler（Services.Shutdown 反序释放 / 测试夹具调用）</summary>
    public void Dispose() => Object.DestroyHandler -= _destroyHandler;

    /// <summary>当前活动场景；LoadScene 后即切换（旧场景对象销毁延后至帧末）。</summary>
    public SilkEngine.Scene.Scene? ActiveScene { get; internal set; }

    /// <summary>
    /// 业务统一创建入口：创建场景并装配显式上下文（注册表 + 资产服务 + 场景归属），
    /// 供 <see cref="SilkEngine.Scene.Scene.CreateGameObject"/> 绑定消费。
    /// </summary>
    /// <param name="name">场景名称</param>
    /// <returns>已绑定上下文的新场景</returns>
    /// <exception cref="InvalidOperationException">注册表尚未注入（EngineLoop.Initialize 前）</exception>
    public SilkEngine.Scene.Scene Create(string name)
    {
        if (_registry is null)
            throw new InvalidOperationException(
                "SceneManager 注册表尚未注入：请先完成 EngineHost.Initialize 再创建场景。"
            );
        var scene = new SilkEngine.Scene.Scene(name);
        scene.Context = new SceneContext(this, _registry, AssetService, scene);
        return scene;
    }

    /// <summary>
    /// 登记场景对象（Scene.CreateGameObject 调用）：对象组件经注入注册表登记并立即生效
    /// （帧末快照 swap 后对派发可见）。
    /// </summary>
    /// <param name="go">要登记的对象</param>
    internal void RegisterSceneObject(GameObject go)
    {
        if (_registry is null)
            return;
        InvokeRecursive(go, c => _registry.Register(c));
        MaterializeRenderers(go); // 对象入场景即获得资产服务 → 渲染器先建立驻留槽（先于帧末驱逐）
        _registry.ApplyPending();
    }

    /// <summary>加载场景（使用注入的注册表）：旧场景根对象进销毁队列，新场景组件立即注册、帧末统一生效。</summary>
    /// <param name="scene">要加载的场景</param>
    public void LoadScene(SilkEngine.Scene.Scene scene) => LoadScene(scene, _registry);

    /// <summary>
    /// 加载场景并显式指定注册表：旧场景根对象进销毁队列（帧末统一销毁），
    /// 新场景全部组件经注册表 Register + ApplyPending（帧末快照 swap 后可见）。
    /// </summary>
    /// <param name="scene">要加载的场景</param>
    /// <param name="registry">组件注册表；null 时用注入的注册表</param>
    public void LoadScene(SilkEngine.Scene.Scene scene, ComponentRegistry? registry = null)
    {
        if (ActiveScene != null)
        {
            foreach (var go in ActiveScene._rootObjects.ToArray())
                Object.Destroy(go); // 统一队列 + 幂等 + 立即失活
        }
        ActiveScene = scene;
        var reg = registry ?? _registry;
        // 无上下文的独立场景在加载时装配上下文（渲染器经上下文解析资产服务；Create 路径已装配），
        // 并传播给已有根对象（AddRootObject 早于 LoadScene 时对象上下文尚未绑定）。
        if (scene.Context is null && reg is not null)
            scene.Context = new SceneContext(this, reg, AssetService, scene);
        foreach (var go in scene._rootObjects)
            go.Context ??= scene.Context;
        if (reg != null)
        {
            foreach (var go in scene._rootObjects)
                InvokeRecursive(go, c => reg.Register(c));
            foreach (var go in scene._rootObjects)
                MaterializeRenderers(go); // 渲染器驻留槽随场景绑定立即物化（帧末驱逐值班后先于首帧收集）
            reg.ApplyPending();
        }
        if (LogConfig.Scene)
            Log.Info($"[Scene] Loaded '{scene.Name}' (roots: {scene.GetRootGameObjects().Length})");
    }

    /// <summary>将当前活动场景的全部组件登记进注册表（EngineLoop 初始化后调用，替代逐对象 AddComponent 注册）。</summary>
    public void RegisterScene()
    {
        if (ActiveScene == null || _registry == null)
            return;
        foreach (var go in ActiveScene._rootObjects)
            InvokeRecursive(go, c => _registry.Register(c));
        foreach (var go in ActiveScene._rootObjects)
            MaterializeRenderers(go);
        _registry.ApplyPending();
    }

    private static void InvokeRecursive(GameObject go, Action<Component> action)
    {
        foreach (var c in go._components)
            action(c);
        foreach (var child in go.Transform.Children)
            InvokeRecursive(child.GameObject!, action);
    }

    /// <summary>
    /// 逻辑帧 Tick：对快照中的活跃组件补发 OnStart（仅一次，首帧）并调用 OnUpdate。
    /// </summary>
    /// <param name="snapshot">当前帧组件快照（双缓冲读侧）</param>
    /// <param name="dt">本帧增量时间（秒）</param>
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

    /// <summary>固定步长 Tick：对快照中的活跃组件调用 OnFixedUpdate。</summary>
    /// <param name="snapshot">当前帧组件快照</param>
    /// <param name="fdt">固定步长（秒）</param>
    public void FixedTick(FrameSnapshot snapshot, float fdt)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnFixedUpdate(fdt);
    }

    /// <summary>逻辑帧末：对快照中的活跃组件调用 OnLateUpdate（OnUpdate 之后）。</summary>
    /// <param name="snapshot">当前帧组件快照</param>
    public void LateTick(FrameSnapshot snapshot)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnLateUpdate();
    }

    /// <summary>渲染提交后：对快照中的活跃组件调用 OnPostRender（LateTick 之后、帧末提交之前）。</summary>
    /// <param name="snapshot">当前帧组件快照</param>
    public void PostRender(FrameSnapshot snapshot)
    {
        foreach (var mb in GetActiveMBs(snapshot))
            mb.OnPostRender();
    }

    private IEnumerable<MonoBehaviour> GetActiveMBs(FrameSnapshot snapshot)
    {
        foreach (var list in snapshot.MbGroups)
            foreach (var mb in list)
                if (
                    mb.GameObject.IsActiveInHierarchy
                    && mb.Enabled
                    && !mb.IsDestroyPending
                    && !mb.GameObject.IsDestroyPending
                )
                    yield return mb;
    }

    /// <summary>
    /// 运行时向活动场景添加 GameObject（含子树）：登记为根对象并立即注册全部组件
    /// （帧末 ApplyPending 生效），不重复触发生命周期。
    /// </summary>
    /// <param name="go">要添加的对象（必须无父级且未在场景中）</param>
    /// <returns>是否添加成功（无活动场景 / 已在场景中 / 有父级时返回 false）</returns>
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
        MaterializeRenderers(go); // 运行时入场景：渲染器驻留槽随上下文（若有）立即物化
        if (LogConfig.Scene)
            Log.Info($"[Scene] Added object '{go.Name}'");
        return true;
    }

    /// <summary>递归物化渲染器驻留槽：对象进入场景获得资产服务后立即建立 Mesh/Texture/材质驻留（幂等）</summary>
    /// <param name="go">要处理的根对象</param>
    private static void MaterializeRenderers(GameObject go)
    {
        foreach (var c in go._components)
            (c as RendererBase)?.MaterializeSlots();
        foreach (var child in go.Transform.Children)
            MaterializeRenderers(child.GameObject!);
    }
}
