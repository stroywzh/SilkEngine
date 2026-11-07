using SilkEngine.Serialization;

namespace SilkEngine.Asset.Abstractions;

internal interface ISidecarCodec<T>
    where T : AssetSidecarBase
{
    T Decode(ReadOnlySpan<byte> content, string sidecarPath);
    byte[] Encode(T sidecar);
}

internal sealed class JsonSidecarCodec<T> : ISidecarCodec<T>
    where T : AssetSidecarBase
{
    public T Decode(ReadOnlySpan<byte> content, string sidecarPath)
        => JsonCodec.Decode<T>(content, sidecarPath);

    public byte[] Encode(T sidecar) => JsonCodec.Encode(sidecar, humanReadable: true);
}
