using SilkEngine.Asset;
using SilkEngine.AssetSystem.Content;
using SilkEngine.AssetSystem.Content.Abstractions;

namespace SilkEngine.AssetSystem.Content.Transfer;

/// <summary>Reference to a content-addressed import artifact.</summary>
internal sealed record ArtifactRef
{
    public required string Hash { get; init; }
    public required long Length { get; init; }
}
