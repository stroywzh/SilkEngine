using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset.Payload;

/// <summary>Mesh CPU 载荷：顶点/索引等原始数据。</summary>
/// <remarks>TODO(asset-wiring): keep the payload type for the eventual mesh loader; nothing produces or consumes
/// it yet.</remarks>
internal sealed class MeshPayload : IAssetPayload
{
    public required ReadOnlyMemory<float> Positions { get; init; }
    public required ReadOnlyMemory<float> Normals { get; init; }
    public required ReadOnlyMemory<uint> Indices { get; init; }

    /// <summary>释放顶点/索引数组；释放后不得再读取本载荷。</summary>
    public void Dispose() { }
}
