using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>Built-in content module: supplies the startup bootstrap that scans, imports and catalogs content.</summary>
internal sealed class ContentAssetModule : IAssetModule
{
    public IAssetBootstrap CreateBootstrap() => new ContentAssetBootstrap();
}
