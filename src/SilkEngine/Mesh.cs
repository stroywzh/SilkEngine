using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine;

/// <summary>Runtime texture asset: size and format description.</summary>
public sealed record Mesh : AssetBase
{
    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    internal Mesh() { }

    /// <summary>Creates the descriptor for an asset already registered in the asset store.</summary>
    public Mesh(AssetId assetId)
        : base(assetId) { }

    public required float[] Positions { get; init; }
    public required float[] Normals { get; init; }
    public required uint[] Indices { get; init; }
}
