using SilkEngine.Assets;
using SilkEngine.Render;

namespace SandBox.Demos;

/// <summary>演示装配帮助：旧数据工厂产物 → 新资产载荷（演示代码专用，不构成运行时 API）。</summary>
public static class DemoAssets
{
    /// <summary>从数据工厂网格创建网格载荷（顶点/布局/索引复制）。</summary>
    /// <param name="mesh">数据工厂网格</param>
    /// <returns>网格载荷</returns>
    public static MeshAsset MeshFrom(Mesh mesh) =>
        new(mesh.Name, mesh.Vertices, mesh.Layout, mesh.Indices);

    /// <summary>演示用随机资产标识（真实路径经 VFS 索引分配）。</summary>
    /// <returns>随机资产标识</returns>
    public static AssetId NewId() => new(Guid.NewGuid());
}
