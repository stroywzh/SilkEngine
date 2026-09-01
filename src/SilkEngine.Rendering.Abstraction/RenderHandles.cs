namespace SilkEngine.Rendering.Abstraction;

/// <summary>纹理 GPU 资源句柄：仅携带后端分配的数值标识，无任何资产身份。</summary>
public readonly record struct RenderTextureHandle(ulong Value);

/// <summary>着色器 GPU 资源句柄：仅携带后端分配的数值标识，无任何资产身份。</summary>
public readonly record struct RenderShaderHandle(ulong Value);

/// <summary>网格 GPU 资源句柄：仅携带后端分配的数值标识，无任何资产身份。</summary>
public readonly record struct RenderMeshHandle(ulong Value);

/// <summary>GPU 资源种类。</summary>
public enum RenderResourceKind
{
    /// <summary>纹理资源。</summary>
    Texture,

    /// <summary>着色器资源。</summary>
    Shader,

    /// <summary>网格资源。</summary>
    Mesh,
}

/// <summary>释放 GPU 资源的请求：仅携带资源种类与句柄数值，无任何资产身份。</summary>
public readonly record struct RenderResourceReleaseRequest(RenderResourceKind Kind, ulong Handle);
