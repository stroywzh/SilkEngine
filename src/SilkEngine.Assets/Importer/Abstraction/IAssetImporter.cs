namespace SilkEngine.Assets.Importer;

/// <summary>
/// 资产导入器：raw 字节 → <see cref="AssetImportResult"/>（Payload + 逻辑路径依赖 + 导入器修订）。
/// 导入器只生成 <see cref="IAssetPayload"/>，不创建 GPU 对象、Scene 组件或运行时实例；
/// 依赖以 <see cref="AssetImportDependency"/>（逻辑路径 + 期望类型）声明，由 Pipeline 在任务 5 解析。
/// </summary>
public interface IAssetImporter
{
    /// <summary>导入资产：raw 字节 → 导入结果；失败时抛出异常。</summary>
    /// <param name="source">原始文件字节（只读视图，实现方不得缓存或修改）</param>
    /// <param name="context">导入上下文（源路径与设置）</param>
    /// <returns>导入结果（载荷 + 依赖 + 修订）</returns>
    AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context);
}
