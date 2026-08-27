using System;
using System.Collections.Generic;
using SilkEngine.Assets;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// TextureAsset → OpenGLTexture 惰性创建缓存（渲染线程专用）
/// 仅维护字典命中语义；GL 资源创建/释放由注入工厂与调用方负责（可数据层单测）
/// </summary>
public sealed class OpenGLTextureRegistry
{
    private readonly Dictionary<TextureAsset, OpenGLTexture> _cache = new();
    private readonly Func<TextureAsset, OpenGLTexture> _factory;

    /// <summary>
    /// 以注入工厂创建缓存条目（工厂实现 GL 创建逻辑，可数据层单测）
    /// </summary>
    /// <param name="factory">TextureAsset → OpenGLTexture 创建工厂</param>
    public OpenGLTextureRegistry(Func<TextureAsset, OpenGLTexture> factory) => _factory = factory;

    /// <summary>缓存条目数</summary>
    internal int Count => _cache.Count;

    /// <summary>全部 GL 纹理（后端 Dispose 统一回收用）</summary>
    public IReadOnlyCollection<OpenGLTexture> Values => _cache.Values;

    /// <summary>取缓存条目；未命中经工厂创建并缓存</summary>
    public OpenGLTexture GetOrCreate(TextureAsset texture)
    {
        if (!_cache.TryGetValue(texture, out var glTex))
        {
            glTex = _factory(texture);
            _cache[texture] = glTex;
        }
        return glTex;
    }

    /// <summary>移除缓存条目（卸载路径；调用方负责对返回值执行 GL 释放）</summary>
    public bool TryRemove(TextureAsset texture, out OpenGLTexture? glTex) =>
        _cache.Remove(texture, out glTex);
}
