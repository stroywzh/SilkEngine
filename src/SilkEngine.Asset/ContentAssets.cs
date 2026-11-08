using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>Public composition entry point for the built-in content pipeline.</summary>
/// <remarks>Bootstrap code (the host's composition root) passes <see cref="CreateBootstrap"/> to
/// <c>EngineOptions.AssetBootstrap</c>; game/business code only uses the <c>Assets</c> facade.</remarks>
public static class ContentAssets
{
    /// <summary>Creates the built-in content bootstrap (scan, import, catalog, attach).</summary>
    public static IAssetBootstrap CreateBootstrap() => new ContentAssetBootstrap();
}
