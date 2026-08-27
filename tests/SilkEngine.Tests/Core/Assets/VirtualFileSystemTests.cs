using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>虚拟文件系统与索引测试：内存文件服务读写、索引增量更新、身份保持与重复提交幂等</summary>
public class VirtualFileSystemTests
{
    [Fact]
    public async Task FileSystem_NormalizesAndReadsLogicalPath()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        fs.Add("Textures/player.png", [1, 2, 3]);

        Assert.Equal("Textures/player.png", fs.Normalize("textures/../Textures/player.png"));
        Assert.Equal(new byte[] { 1, 2, 3 },
            (await fs.ReadAsync("Textures/player.png")).ToArray());
    }

    [Fact]
    public void Index_ApplyIsIdempotentAndReportsChanges()
    {
        var index = new InMemoryVirtualFileIndex();
        var scan = ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]);

        var first = index.Apply(scan);
        var second = index.Apply(scan);

        Assert.Contains(first.Changes, c => c.Kind == VirtualChangeKind.Added);
        Assert.Empty(second.Changes);
    }

    [Fact]
    public void FileSystem_Add_RejectsEscapeOutsideRoot()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        Assert.Throws<ArgumentException>(() => { fs.Add("../outside.png", [1]); });
    }

    [Fact]
    public void FileSystem_Exists_ReflectsAddedFiles()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        fs.Add("Textures/player.png", [1, 2, 3]);

        Assert.True(fs.Exists("Textures/player.png"));
        Assert.False(fs.Exists("Textures/missing.png"));
    }

    [Fact]
    public async Task FileSystem_ReadMissingFile_ThrowsFileNotFoundException()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        await Assert.ThrowsAsync<FileNotFoundException>(() => fs.ReadAsync("missing.png").AsTask());
    }

    [Fact]
    public async Task FileSystem_Overwrite_BumpsVersionAndReturnsLatestContent()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        fs.Add("a.png", [1]);
        fs.Add("a.png", [2, 2]);

        var meta = await fs.GetMetadataAsync("a.png");
        Assert.Equal(2L, meta.Length);
        Assert.Equal(2UL, meta.Version);
        Assert.Equal(new byte[] { 2, 2 }, (await fs.ReadAsync("a.png")).ToArray());
    }

    [Fact]
    public void Index_Add_CreatesNodesWithParentLinks()
    {
        var index = new InMemoryVirtualFileIndex();
        var scan = ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]);

        var result = index.Apply(scan);

        Assert.Equal(2, result.Changes.Count(c => c.Kind == VirtualChangeKind.Added));
        Assert.True(index.TryGet("Assets", out var dir));
        Assert.True(index.TryGet("Assets/a.png", out var file));
        Assert.Null(dir!.ParentId);
        Assert.Equal(dir.Id, file!.ParentId);
        Assert.NotEqual(dir.Id, file.Id);
    }

    [Fact]
    public void Index_EnumerateChildren_ListsImmediateChildren()
    {
        var index = new InMemoryVirtualFileIndex();
        var scan = ScanResult.FromFiles([
            ScanFile.Directory("Assets"),
            ScanFile.Directory("Assets/Textures"),
            ScanFile.File("Assets/a.png", 1),
            ScanFile.File("Assets/Textures/player.png", 1),
        ]);
        index.Apply(scan);

        var children = index.EnumerateChildren("Assets").ToList();

        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c.LogicalPath == "Assets/Textures");
        Assert.Contains(children, c => c.LogicalPath == "Assets/a.png");
    }

    [Fact]
    public void Index_EnumerateChildren_UnknownDirectory_ReturnsEmpty()
    {
        var index = new InMemoryVirtualFileIndex();
        Assert.Empty(index.EnumerateChildren("Missing"));
    }

    [Fact]
    public void Index_Modify_KeepsIdAndBumpsRevision()
    {
        var index = new InMemoryVirtualFileIndex();
        var first = index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]));
        var id = first.Changes.Single(c => c.LogicalPath == "Assets/a.png").NodeId;

        var second = index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 2)]));

        var change = Assert.Single(second.Changes);
        Assert.Equal(VirtualChangeKind.Modified, change.Kind);
        Assert.Equal(id, change.NodeId);
        Assert.True(index.TryGet("Assets/a.png", out var node));
        Assert.Equal(id, node!.Id);
        Assert.Equal(2UL, node.Revision);
    }

    [Fact]
    public void Index_Remove_ReportsOnceThenStaysQuiet()
    {
        var index = new InMemoryVirtualFileIndex();
        var withFile = ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]);
        var withoutFile = ScanResult.FromFiles([ScanFile.Directory("Assets")]);
        index.Apply(withFile);

        var removal = index.Apply(withoutFile);

        var change = Assert.Single(removal.Changes);
        Assert.Equal(VirtualChangeKind.Removed, change.Kind);
        Assert.Equal("Assets/a.png", change.LogicalPath);
        Assert.Empty(index.Apply(withoutFile).Changes);
    }

    [Fact]
    public void Index_RemoveThenReadd_CreatesNewNode()
    {
        var index = new InMemoryVirtualFileIndex();
        index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]));
        index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets")]));

        var readd = index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]));

        Assert.Contains(readd.Changes, c => c.Kind == VirtualChangeKind.Added && c.LogicalPath == "Assets/a.png");
    }

    [Fact]
    public void Index_Move_WithPreviousPath_KeepsNodeId()
    {
        var index = new InMemoryVirtualFileIndex();
        var first = index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]));
        var id = first.Changes.Single(c => c.LogicalPath == "Assets/a.png").NodeId;

        var move = index.Apply(ScanResult.FromFiles([
            ScanFile.Directory("Assets"),
            ScanFile.File("Assets/b.png", 2, previousPath: "Assets/a.png"),
        ]));

        var change = Assert.Single(move.Changes);
        Assert.Equal(VirtualChangeKind.Moved, change.Kind);
        Assert.Equal(id, change.NodeId);
        Assert.Equal("Assets/b.png", change.LogicalPath);
        Assert.Equal("Assets/a.png", change.PreviousPath);
        Assert.True(index.TryGet("Assets/b.png", out var node));
        Assert.Equal(id, node!.Id);
        Assert.False(index.TryGet("Assets/a.png", out _));
    }

    [Fact]
    public void Index_Move_WithoutResolvableIdentity_IsAdded()
    {
        var index = new InMemoryVirtualFileIndex();
        var scan = ScanResult.FromFiles([
            ScanFile.Directory("Assets"),
            ScanFile.File("Assets/b.png", 1, previousPath: "Assets/missing.png"),
        ]);

        var result = index.Apply(scan);

        Assert.Contains(result.Changes, c => c.Kind == VirtualChangeKind.Added && c.LogicalPath == "Assets/b.png");
    }

    [Fact]
    public void Index_TryGet_ByPathAndById_ReturnSameNode()
    {
        var index = new InMemoryVirtualFileIndex();
        index.Apply(ScanResult.FromFiles([ScanFile.Directory("Assets"), ScanFile.File("Assets/a.png", 1)]));

        Assert.True(index.TryGet("Assets/a.png", out var byPath));
        Assert.True(index.TryGet(byPath!.Id, out var byId));
        Assert.Equal(byPath, byId);
    }
}
