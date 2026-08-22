using System;
using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>GPU 资源注册中心：按资产实例引用寻键（同实例复用、不同实例独立），支持驱逐与全量释放。</summary>
public interface IGpuResourceRegistry
{
    /// <summary>
    /// 按资产实例引用获取 GPU 对象；未命中经 factory 创建并缓存（同实例复用、不同实例独立）
    /// </summary>
    /// <typeparam name="TAsset">资产类型</typeparam>
    /// <typeparam name="TGpu">GPU 资源类型（IDisposable）</typeparam>
    /// <param name="asset">资产实例（按引用寻键）</param>
    /// <param name="factory">未命中时创建 GPU 对象的工厂</param>
    /// <returns>已缓存或新建的 GPU 对象</returns>
    TGpu GetOrCreate<TAsset, TGpu>(TAsset asset, Func<TAsset, TGpu> factory)
        where TAsset : class
        where TGpu : IDisposable;

    /// <summary>驱逐指定资产的 GPU 对象并对其执行 Dispose（资产卸载路径）</summary>
    /// <param name="asset">待驱逐的资产实例</param>
    void Evict(IAsset asset);

    /// <summary>释放全部 GPU 对象并清空注册表（后端 Dispose 统一回收）</summary>
    void ReleaseAll();
}
