using SilkEngine.Asset;

namespace SilkEngine;

/// <summary>Runtime mesh asset: vertex/index data description.</summary>
/// <remarks>TODO(asset-wiring): no mesh loader, importer sidecar or render uploader is wired yet. Keep the type
/// here with the other engine runtime assets; wire loading/upload in a later phase.</remarks>
public sealed class Mesh : AssetBase
{
    /// <inheritdoc />
    public override string AssetTypeName => MeshAssetName;

    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    internal Mesh()
        : base() { }

    /// <summary>Creates the descriptor bound to its store-issued handle.</summary>
    internal Mesh(AssetHandle handle)
        : base(handle) { }

    public required float[] Positions { get; init; }
    public required float[] Normals { get; init; }
    public required uint[] Indices { get; init; }
}
