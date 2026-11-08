using SilkEngine.Asset;

namespace SilkEngine;

/// <summary>Runtime mesh asset: vertex/index data description.</summary>
/// <remarks>TODO(asset-wiring): no mesh loader, importer sidecar or render uploader is wired yet. Keep the type
/// here with the other engine runtime assets; wire loading/upload in a later phase.</remarks>
public sealed class Mesh : AssetBase
{
    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    internal Mesh()
        : base() { }

    /// <summary>Creates the descriptor for an asset already registered in the asset store.</summary>
    public Mesh(AssetId assetId)
        : base(assetId) { }

    public required float[] Positions { get; init; }
    public required float[] Normals { get; init; }
    public required uint[] Indices { get; init; }
}
