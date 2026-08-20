namespace SilkEngine.Core.Assets.Importer;

/// <summary>资产导入器：raw 字节 → 资产实例</summary>
public interface IAssetImporter
{
    /// <summary>导入资产；失败时抛出异常</summary>
    IAsset Import(byte[] raw, ImportSettings? settings = null);
}
