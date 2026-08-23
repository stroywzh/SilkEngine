namespace SilkEngine.Tests.Core;

/// <summary>临时 PNG 文件助手：唯一路径写入 fixture，Dispose 时删除</summary>
internal sealed class PngTestFile : IDisposable
{
    public string FilePath { get; }

    private PngTestFile(string filePath) => FilePath = filePath;

    public static PngTestFile Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-asset-{Guid.NewGuid():N}.png");
        System.IO.File.WriteAllBytes(path, PngFixtures.RedPng);
        return new PngTestFile(path);
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(FilePath))
            System.IO.File.Delete(FilePath);
    }
}
