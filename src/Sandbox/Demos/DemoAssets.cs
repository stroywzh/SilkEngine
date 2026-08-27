using SilkEngine.Assets;

namespace SandBox.Demos;

/// <summary>演示装配帮助：演示代码专用，不构成运行时 API。</summary>
public static class DemoAssets
{
    /// <summary>演示用随机资产标识（真实路径经 VFS 索引分配）。</summary>
    /// <returns>随机资产标识</returns>
    public static AssetId NewId() => new(Guid.NewGuid());
}
