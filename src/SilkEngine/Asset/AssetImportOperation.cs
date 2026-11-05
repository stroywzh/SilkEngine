namespace SilkEngine.Asset;

/// <summary>Import result: sidecar path, metadata and whether anything was written.</summary>
public sealed record ImportResult(string SidecarPath, TextureSidecar Metadata, bool Changed);

//TODO: 这是针对资产导入的封装
public sealed record AssetImpoerOpeartion
{
    public ImportResult ImportResult;
}