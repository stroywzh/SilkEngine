using System;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render;

/// <summary>GPU 资源注册中心：按资产实例引用寻键（同实例复用、不同实例独立），支持驱逐与全量释放。</summary>
public interface IGpuResourceRegistry
{
    TGpu GetOrCreate<TAsset, TGpu>(TAsset asset, Func<TAsset, TGpu> factory)
        where TAsset : class
        where TGpu : IDisposable;
    void Evict(IAsset asset);
    void ReleaseAll();
}
