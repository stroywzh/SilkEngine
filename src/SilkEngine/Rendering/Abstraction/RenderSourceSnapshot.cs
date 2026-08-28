using System.Collections.Generic;
using SilkEngine.Render;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 渲染源快照：Main 域一次性构建的只读相机/渲染器视图（无资产语义，供收集与管线消费）。
/// 由 Scene 域 <see cref="Scene.SceneRenderWorld"/> 从帧快照构建；EngineLoop 不查询具体场景类型。
/// </summary>
/// <param name="Cameras">活跃相机视图列表（首个即当前相机；恒非空——含默认相机回退）</param>
/// <param name="Renderers">可渲染对象列表（来自已注册 provider；可为空）</param>
public sealed record RenderSourceSnapshot(
    IReadOnlyList<ICameraView> Cameras,
    IReadOnlyList<IRenderable> Renderers);
