namespace SilkEngine.Assets;

/// <summary>CPU 侧 2D 纹理资产（GL 资源由渲染线程惰性创建，见 Part 3）；过渡期兼容类型，由后续计划删除</summary>
public sealed class Texture2D : IAsset
{
    /// <summary>纹理名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>图像数据</summary>
    public ImageData Data { get; init; } = new(0, 0, []);
}
