using System;
using Silk.NET.OpenGL;
using SilkEngine.Assets;
using SilkEngine.Math;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// IMaterial 的 OpenGL 实现
/// <br/>在渲染线程绑定着色器并设置材质绑定载荷（<see cref="BoundMaterialValue"/>）的 uniform 值
/// <br/>只消费只读绑定数据，绝不写回材质实例或材质资产
/// </summary>
public class OpenGLMaterial : IMaterial
{
    /// <summary>主纹理采样器 uniform 名</summary>
    public const string SamplerUniformName = "uMainTex";

    private readonly GL _gl;
    private readonly BoundMaterialValue _data;
    private readonly OpenGLShader _shader;
    private readonly OpenGLTextureRegistry _textures;
    private readonly Func<AssetHandle<TextureAsset>, Texture2D?>? _textureResolver;
    private bool _disposed;

    /// <summary>
    /// 从绑定载荷创建 OpenGL 材质，绑定指定着色器
    /// </summary>
    /// <param name="gl">OpenGL API 实例</param>
    /// <param name="data">绑定就绪载荷（只读参数快照 + 依赖句柄）</param>
    /// <param name="shader">已编译着色器</param>
    /// <param name="textures">纹理注册中心</param>
    /// <param name="textureResolver">纹理句柄 → Texture2D 解析委托（缺省 null → 白色占位回落）</param>
    public OpenGLMaterial(
        GL gl,
        BoundMaterialValue data,
        OpenGLShader shader,
        OpenGLTextureRegistry textures,
        Func<AssetHandle<TextureAsset>, Texture2D?>? textureResolver = null)
    {
        _gl = gl;
        _data = data;
        _shader = shader;
        _textures = textures;
        _textureResolver = textureResolver;
    }

    /// <inheritdoc />
    public void Apply()
    {
        _shader.Use();
        foreach (var (name, value) in _data.Parameters)
        {
            int loc = _gl.GetUniformLocation(_shader.GetProgram(), name);
            if (loc == -1)
                continue;
            switch (value.Kind)
            {
                case MaterialValue.ValueKind.Float:
                    if (value.TryGetFloat(out var f))
                        _gl.Uniform1(loc, f);
                    break;
                case MaterialValue.ValueKind.Vector3:
                    if (value.TryGetVector3(out var v))
                        _gl.Uniform3(loc, v.X, v.Y, v.Z);
                    break;
                case MaterialValue.ValueKind.Matrix4x4:
                    if (value.TryGetMatrix4x4(out var m))
                        UploadMatrix(loc, m);
                    break;
            }
        }

        int samplerLoc = _gl.GetUniformLocation(_shader.GetProgram(), SamplerUniformName);
        if (samplerLoc != -1)
        {
            var texture = ResolveTexture(_data, _textureResolver);
            var glTex = _textures.GetOrCreate(texture);
            glTex.EnsureCreated(_gl);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, glTex.Handle);
            _gl.Uniform1(samplerLoc, 0);
        }
    }

    /// <summary>
    /// 解析绑定实际纹理：无主纹理句柄（含解析未命中）→ 引擎白色占位
    /// </summary>
    /// <param name="material">绑定就绪载荷</param>
    /// <param name="resolver">纹理句柄 → Texture2D 解析委托（可为 null）</param>
    /// <returns>绑定的纹理；无主纹理或解析失败时为白色占位</returns>
    internal static Texture2D ResolveTexture(
        BoundMaterialValue material,
        Func<AssetHandle<TextureAsset>, Texture2D?>? resolver) =>
        material.MainTexture is { } handle && resolver?.Invoke(handle) is { } texture
            ? texture
            : DefaultTextures.White;

    private unsafe void UploadMatrix(int loc, Matrix4x4 m)
    {
        var matrix = m;
        float* ptr = &matrix.M11;   // Matrix4x4 为 Sequential 布局（16 个连续 float），栈上局部零分配直传
        _gl.UniformMatrix4(loc, 1, true, ptr);   // GL 上传 transpose=true（列主序约定）
    }

    /// <inheritdoc />
    /// <summary>释放材质状态（幂等；当前无自有 GL 资源，仅置标志）</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
