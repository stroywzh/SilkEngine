namespace SilkEngine.Assets;

// TODO: 重新设计
public interface IAssetData<T> : IAsset
    where T : IAssetDataRaw
{
    T Data { get; init; }
}

public interface IAssetDataRaw
{
    byte[] RawBytes { get; init; }
}
