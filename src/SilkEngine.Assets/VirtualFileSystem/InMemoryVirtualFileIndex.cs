using SilkEngine.Assets;

namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>内存虚拟文件索引：逻辑路径索引 + 节点 ID 索引；首次新增分配 ID，修改/移动保留 ID 并递增 revision，重复提交无变更，删除保留 tombstone</summary>
public sealed class InMemoryVirtualFileIndex : IVirtualFileIndex
{
    private readonly Dictionary<string, VirtualNode> _byPath = new(StringComparer.Ordinal);
    private readonly Dictionary<VirtualNodeId, VirtualNode> _byId = [];
    private readonly Dictionary<string, VirtualNodeId> _tombstones = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(string logicalPath, out VirtualNode? node) => _byPath.TryGetValue(logicalPath, out node);

    /// <inheritdoc />
    public bool TryGet(VirtualNodeId id, out VirtualNode? node) => _byId.TryGetValue(id, out node);

    /// <inheritdoc />
    public IEnumerable<VirtualNode> EnumerateChildren(string directoryPath)
    {
        if (!_byPath.TryGetValue(directoryPath, out var directory) || directory.NodeType != VirtualNodeType.Directory)
            return [];
        var parentId = directory.Id;
        return _byId.Values.Where(n => n.ParentId == parentId);
    }

    /// <inheritdoc />
    public VirtualIndexApplyResult Apply(ScanResult scan)
    {
        var changes = new List<VirtualChange>();
        var scanned = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in scan.Files)
        {
            scanned.Add(file.LogicalPath);

            if (file.PreviousPath is { } previous
                && _byPath.TryGetValue(previous, out var moved)
                && moved.LogicalPath != file.LogicalPath)
            {
                MoveNode(moved, file);
                changes.Add(new VirtualChange(VirtualChangeKind.Moved, moved.Id, file.LogicalPath, previous));
                continue;
            }

            if (_byPath.TryGetValue(file.LogicalPath, out var node))
            {
                if (node.NodeType == file.NodeType && IsSameVersion(node, file))
                    continue;
                UpdateNode(node, file);
                changes.Add(new VirtualChange(VirtualChangeKind.Modified, node.Id, file.LogicalPath));
                continue;
            }

            _tombstones.Remove(file.LogicalPath);
            var created = CreateNode(file);
            changes.Add(new VirtualChange(VirtualChangeKind.Added, created.Id, file.LogicalPath));
        }

        foreach (var path in _byPath.Keys.ToArray())
        {
            if (scanned.Contains(path))
                continue;
            var node = _byPath[path];
            _byPath.Remove(path);
            _byId.Remove(node.Id);
            _tombstones[path] = node.Id;
            changes.Add(new VirtualChange(VirtualChangeKind.Removed, node.Id, path));
        }

        return new VirtualIndexApplyResult(changes);
    }

    private static bool IsSameVersion(VirtualNode node, ScanFile file)
    {
        if (file.NodeType == VirtualNodeType.Directory)
            return true;
        return node.MetaData?.FileHash == file.Version
            && node.MetaData?.SourceFingerprint == file.SourceFingerprint;
    }

    private VirtualNode CreateNode(ScanFile file)
    {
        var node = new VirtualNode
        {
            Id = new VirtualNodeId(Guid.NewGuid()),
            ParentId = ResolveParentId(file.LogicalPath),
            NodeType = file.NodeType,
            LogicalPath = file.LogicalPath,
            Revision = 1,
            MetaData = BuildMetaData(file),
        };
        _byPath[node.LogicalPath] = node;
        _byId[node.Id] = node;
        return node;
    }

    private void UpdateNode(VirtualNode node, ScanFile file)
    {
        var updated = node with
        {
            NodeType = file.NodeType,
            Revision = node.Revision + 1,
            MetaData = BuildMetaData(file),
        };
        _byPath[file.LogicalPath] = updated;
        _byId[node.Id] = updated;
    }

    private void MoveNode(VirtualNode node, ScanFile file)
    {
        var moved = node with
        {
            ParentId = ResolveParentId(file.LogicalPath),
            NodeType = file.NodeType,
            LogicalPath = file.LogicalPath,
            Revision = node.Revision + 1,
            MetaData = BuildMetaData(file),
        };
        _byPath.Remove(node.LogicalPath);
        _byPath[file.LogicalPath] = moved;
        _byId[node.Id] = moved;
    }

    private VirtualNodeId? ResolveParentId(string logicalPath)
    {
        var slash = logicalPath.LastIndexOf('/');
        if (slash < 0)
            return null;
        var parentPath = logicalPath[..slash];
        return _byPath.TryGetValue(parentPath, out var parent) ? parent.Id : null;
    }

    private static MetaDataModel BuildMetaData(ScanFile file) => new()
    {
        LogicPath = file.LogicalPath,
        FileHash = file.NodeType == VirtualNodeType.File ? file.Version : null,
        SourceFingerprint = file.NodeType == VirtualNodeType.File ? file.SourceFingerprint : null,
    };
}
