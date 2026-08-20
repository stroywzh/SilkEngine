namespace SilkEngine.Core.Assets.Importer;

/// <summary>
/// 导入设置（签名扩展点；采样/翻转等参数由后续 Part 扩展）
/// </summary>
public sealed class ImportSettings
{
    /// <summary>资产源路径（导入器据此派生资产名）</summary>
    public string? Path { get; init; }
}
