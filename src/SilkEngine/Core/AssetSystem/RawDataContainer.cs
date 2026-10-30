namespace SilkEngine.Core.AssetSystem;

/// <summary>Asset payload container: CPU bytes decoded from the source file.</summary>
public sealed class RawDataContainer
{
    /// <summary>Wraps a buffer the caller guarantees will not be reused.</summary>
    public RawDataContainer(ReadOnlyMemory<byte> serialized)
    {
        Serialized = serialized;
    }

    /// <summary>CPU payload; the buffer must not be reused while the container lives.</summary>
    public ReadOnlyMemory<byte> Serialized { get; }

    /// <summary>Whether the data was uploaded to the GPU; set by the load layer.</summary>
    public bool IsLoaded { get; internal set; }
}
