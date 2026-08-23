using System;
using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>引擎内置占位纹理（懒创建，无文件依赖）</summary>
public static class DefaultTextures
{
    private static readonly Lazy<Texture2D> WhiteLazy = new(() =>
        new Texture2D
        {
            Name = "DefaultWhite",
            Data = new ImageData(1, 1, [255, 255, 255, 255]),
        });

    /// <summary>1×1 纯白占位纹理（无主纹理 / LazyAsync 未就绪时绑定）</summary>
    public static Texture2D White => WhiteLazy.Value;
}
