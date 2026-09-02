namespace SilkEngine.Assets.Importer;

/// <summary>HLSL 着色器导入器：校验入口函数后输出不可变 <see cref="ShaderAsset"/>（Source 保持原文）</summary>
public sealed class ShaderImporter : IAssetImporter
{
    private const ulong Revision = 1;

    /// <summary>导入：校验 vert/frag 入口与返回语义，输出不可变单源码着色器载荷。</summary>
    /// <param name="source">HLSL 源码字节（UTF-8 文本）</param>
    /// <param name="context">导入上下文（Path 用于派生资产名与错误定位）</param>
    /// <returns>着色器导入结果</returns>
    /// <exception cref="InvalidDataException">入口校验失败（消息含缺失入口名）</exception>
    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        var text = System.Text.Encoding.UTF8.GetString(source.Span);
        try
        {
            HlslEntryPointValidator.Validate(text);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException((context.Path is { Length: > 0 } ? $"[{context.Path}] " : "") + ex.Message, ex);
        }

        var name = context.Path is { Length: > 0 } path
            ? Path.GetFileNameWithoutExtension(path)
            : "Shader";
        return new AssetImportResult(new ShaderAsset(name, text), [], Revision);
    }
}