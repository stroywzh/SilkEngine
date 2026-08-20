namespace SilkEngine.Core.Assets.Importer;

/// <summary>资产导入器：raw 字节 → 资产实例</summary>
public interface IAssetImporter
{
    /// <summary>导入资产：raw 字节 → 资产实例；失败时抛出异常。</summary>
    /// <param name="raw">原始文件字节</param>
    /// <param name="settings">导入设置（可为 null）</param>
    /// <returns>资产实例</returns>
    IAsset Import(byte[] raw, ImportSettings? settings = null);
}
