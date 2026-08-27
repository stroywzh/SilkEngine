using SilkEngine.Assets.VirtualFileSystem;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>磁盘文件系统测试：真实临时目录读写、路径校验、元数据</summary>
public class DiskAssetFileSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"se-diskfs-{Guid.NewGuid():N}");

    public DiskAssetFileSystemTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Normalize_RejectsAbsolutePathAndRootEscape()
    {
        var fs = new DiskAssetFileSystem(_root);

        Assert.Throws<ArgumentException>(() => fs.Normalize("C:/a.png"));
        Assert.Throws<ArgumentException>(() => fs.Normalize("../outside.png"));
        Assert.Equal("Textures/a.png", fs.Normalize("Textures/../Textures/a.png"));
    }

    [Fact]
    public async Task ReadAsync_ReadsPhysicalFileUnderRoot()
    {
        var fs = new DiskAssetFileSystem(_root);
        Directory.CreateDirectory(Path.Combine(_root, "Textures"));
        await File.WriteAllBytesAsync(Path.Combine(_root, "Textures", "a.png"), [1, 2, 3]);

        var bytes = (await fs.ReadAsync("Textures/a.png")).ToArray();

        Assert.Equal([1, 2, 3], bytes);
        Assert.True(fs.Exists("Textures/a.png"));
        Assert.False(fs.Exists("missing.png"));
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var fs = new DiskAssetFileSystem(_root);

        await Assert.ThrowsAsync<FileNotFoundException>(() => fs.ReadAsync("missing.png").AsTask());
    }

    [Fact]
    public async Task GetMetadataAsync_ReportsLengthAndWriteTime()
    {
        var fs = new DiskAssetFileSystem(_root);
        await File.WriteAllBytesAsync(Path.Combine(_root, "a.png"), [1, 2, 3]);

        var meta = await fs.GetMetadataAsync("a.png");

        Assert.Equal(3L, meta.Length);
        Assert.Equal(DateTime.UtcNow.Date, meta.LastWriteTimeUtc.Date);
    }
}
