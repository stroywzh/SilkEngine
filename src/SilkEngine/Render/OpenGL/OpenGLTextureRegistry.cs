using System;
using System.Collections.Generic;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// Texture2D → OpenGLTexture 惰性创建缓存（渲染线程专用）
/// 仅维护字典命中语义；GL 资源创建/释放由注入工厂与调用方负责（可数据层单测）
/// </summary>
public sealed class OpenGLTextureRegistry
{
    private readonly Dictionary<Texture2D, OpenGLTexture> _cache = new();
    private readonly Func<Texture2D, OpenGLTexture> _factory;

    public OpenGLTextureRegistry(Func<Texture2D, OpenGLTexture> factory) => _factory = factory;

    /// <summary>缓存条目数</summary>
    internal int Count => _cache.Count;

    /// <summary>全部 GL 纹理（后端 Dispose 统一回收用）</summary>
    public IReadOnlyCollection<OpenGLTexture> Values => _cache.Values;

    /// <summary>取缓存条目；未命中经工厂创建并缓存</summary>
    public OpenGLTexture GetOrCreate(Texture2D texture)
    {
        if (!_cache.TryGetValue(texture, out var glTex))
        {
            glTex = _factory(texture);
            _cache[texture] = glTex;
        }
        return glTex;
    }

    /// <summary>移除缓存条目（卸载路径；调用方负责对返回值执行 GL 释放）</summary>
    public bool TryRemove(Texture2D texture, out OpenGLTexture? glTex) =>
        _cache.Remove(texture, out glTex);
}
