using System.Collections.Generic;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 渲染器收集提供者：向 <see cref="Rendering.Pipeline.RenderCollector"/> 提供本帧可渲染对象。
/// 新增 Renderer 类型通过实现并注册 provider 接入收集，不修改 EngineLoop。
/// 本接口无场景依赖；场景查询实现由宿主在组合根注册。
/// </summary>
public interface IRendererProvider
{
    /// <summary>收集本帧可渲染对象（惰性枚举；仅在收集阶段消费）。</summary>
    /// <returns>可渲染对象序列（可为空）</returns>
    IEnumerable<IRenderable> Collect();
}
