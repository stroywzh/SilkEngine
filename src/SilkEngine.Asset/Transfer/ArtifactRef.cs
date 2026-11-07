namespace SilkEngine.Asset.Transfer;

/// <summary>Reference to a content-addressed import artifact.</summary>
internal sealed record ArtifactRef
{
    public required string Hash { get; init; }
    public required long Length { get; init; }
}

