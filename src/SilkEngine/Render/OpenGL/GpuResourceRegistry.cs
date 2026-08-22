using System;
using System.Collections.Generic;
using SilkEngine.Assets;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// GPU 资源注册中心（渲染线程专用）：以资产实例引用为键，同实例复用 GPU 对象、不同实例各自独立。
/// 引用语义经 ReferenceEqualityComparer 延续 0.3 缓存键约定（Shader 同名不同实例不串用）。
/// <br/>Evict/ReleaseAll 驱逐时对 GPU 对象执行 Dispose（驱逐回调），供资产卸载路径与后端释放接入。
/// </summary>
public sealed class GpuResourceRegistry : IGpuResourceRegistry
{
    private readonly Dictionary<object, IDisposable> _entries =
        new(ReferenceEqualityComparer.Instance);

    /// <inheritdoc />
    public TGpu GetOrCreate<TAsset, TGpu>(TAsset asset, Func<TAsset, TGpu> factory)
        where TAsset : class
        where TGpu : IDisposable
    {
        if (_entries.TryGetValue(asset, out var existing))
            return (TGpu)existing;
        var created = factory(asset);
        _entries[asset] = created;
        return created;
    }

    /// <inheritdoc />
    public void Evict(IAsset asset)
    {
        if (_entries.Remove(asset, out var gpu))
            gpu.Dispose();
    }

    /// <inheritdoc />
    public void ReleaseAll()
    {
        foreach (var gpu in _entries.Values)
            gpu.Dispose();
        _entries.Clear();
    }
}
