namespace SilkEngine.Assets;

/// <summary>纹理资产载荷：CPU 侧图像数据容器（GL 资源由渲染侧惰性创建）</summary>
/// <param name="name">纹理名称</param>
/// <param name="data">图像数据</param>
public sealed class TextureAsset(string name, ImageData data) : IAssetPayload
{
    /// <summary>纹理名称</summary>
    public string Name { get; } = name;

    /// <summary>图像数据</summary>
    public ImageData Data { get; } = data;
}
