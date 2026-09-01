namespace SilkEngine.Assets.Importer;

/// <summary>纹理导入器：委托 IImageDecoder 解码为 <see cref="TextureAsset"/> 载荷</summary>
public sealed class TextureImporter : IAssetImporter
{
    private readonly IImageDecoder _decoder;

    /// <summary>创建纹理导入器</summary>
    /// <param name="decoder">解码器（构造注入）</param>
    /// <param name="settings">导入设置（当前未使用，保留为扩展点）</param>
    public TextureImporter(IImageDecoder decoder, ImportSettings? settings = null)
    {
        _decoder = decoder;
    }

    /// <summary>导入：解码为 TextureAsset（Name 取自 context.Path 的文件名；无路径时回退 "Texture"）。</summary>
    /// <param name="source">原始文件字节</param>
    /// <param name="context">导入上下文（Path 用于派生资产名）</param>
    /// <returns>解码完成的纹理导入结果</returns>
    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        var data = _decoder.Decode(source.ToArray());
        var name = context.Path is { Length: > 0 } path
            ? Path.GetFileNameWithoutExtension(path)
            : "Texture";
        return new AssetImportResult(new TextureAsset(name, data), [], ImporterRevision: 1);
    }
}
