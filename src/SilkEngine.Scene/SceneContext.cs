using SilkEngine.Assets;

namespace SilkEngine.Scene;

/// <summary>
/// 场景上下文：SceneManager.Create 为场景装配的显式依赖包（Registry、AssetService 与所属 Scene）。
/// 业务经 <see cref="Scene.CreateGameObject"/> 创建的对象绑定本上下文，
/// 组件挂载与注册不再依赖 Services 回退链（阶段 4 将彻底移除回退）。
/// </summary>
internal sealed class SceneContext
{
    /// <summary>所属场景管理器（对象登记/销毁与场景归属）。</summary>
    public SceneManager Manager { get; }

    /// <summary>组件注册表（场景对象的组件登记目标）。</summary>
    public ComponentRegistry Registry { get; }

    /// <summary>资产服务（渲染器资产槽驻留消费；无资产场景为 null）。</summary>
    public AssetManager? AssetService { get; }

    /// <summary>所属场景。</summary>
    public Scene Scene { get; }

    /// <summary>创建场景上下文。</summary>
    /// <param name="manager">场景管理器</param>
    /// <param name="registry">组件注册表</param>
    /// <param name="assetService">资产服务（可为 null）</param>
    /// <param name="scene">所属场景</param>
    public SceneContext(SceneManager manager, ComponentRegistry registry, AssetManager? assetService, Scene scene)
    {
        Manager = manager;
        Registry = registry;
        AssetService = assetService;
        Scene = scene;
    }
}
