namespace SilkEngine.Core.AssetSystem;

/// <summary>Built-in content module the engine host resolves at startup.</summary>
/// <remarks>The host lives in the engine assembly and must not reference the content assembly, so the
/// built-in module is resolved by name at initialization time.</remarks>
internal interface IAssetModule
{
    IAssetBootstrap CreateBootstrap();
}
