namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>
/// 磁盘轮询变更源：按固定间隔对 <see cref="IAssetFileSystem"/> 执行低频全量扫描，
/// 以源内容指纹（SHA-256）与上一轮观察基线做差集，收敛为新增/修改/删除变更快照。
/// 首次 <see cref="Poll"/> 只建立基线不产生变更（启动扫描已由管线装配）；
/// 间隔未到、或重扫与基线一致时返回 <see cref="ChangeSourceResult.Empty"/>。
/// 不依赖平台 FileSystemWatcher：磁盘事件经扫描收敛为变更快照，由 Main 驱动消费，测试可注入内存源替换。
/// </summary>
public sealed class PollingAssetChangeSource : IAssetChangeSource
{
    private readonly IAssetFileSystem _files;
    private readonly TimeSpan _interval;
    private readonly object _gate = new();
    private Dictionary<string, string>? _baseline;
    private DateTime _lastPollUtc;

    /// <summary>创建磁盘轮询变更源。</summary>
    /// <param name="files">被轮询的资产文件服务（扫描 + 内容指纹来源）</param>
    /// <param name="interval">固定探测间隔；小于等于 0 时每次 <see cref="Poll"/> 都重扫（测试可用）</param>
    /// <exception cref="ArgumentNullException">files 为 null 时抛出</exception>
    public PollingAssetChangeSource(IAssetFileSystem files, TimeSpan interval)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _interval = interval > TimeSpan.Zero ? interval : TimeSpan.Zero;
    }

    /// <inheritdoc />
    public ChangeSourceResult Poll()
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (_baseline is not null && now - _lastPollUtc < _interval)
                return ChangeSourceResult.Empty;
            _lastPollUtc = now;

            var scan = _files.Scan();
            var observed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in scan.Files)
            {
                if (file.NodeType != VirtualNodeType.File)
                    continue;
                observed[file.LogicalPath] = file.SourceFingerprint ?? string.Empty;
            }

            if (_baseline is null)
            {
                _baseline = observed;
                return ChangeSourceResult.Empty;
            }

            var changes = new List<AssetChangeEvent>();
            foreach (var (path, fingerprint) in observed)
            {
                if (_baseline.TryGetValue(path, out var previous))
                {
                    if (!string.Equals(previous, fingerprint, StringComparison.Ordinal))
                        changes.Add(new AssetChangeEvent(AssetChangeKind.Modified, path));
                }
                else
                {
                    changes.Add(new AssetChangeEvent(AssetChangeKind.Added, path));
                }
            }
            foreach (var path in _baseline.Keys)
            {
                if (!observed.ContainsKey(path))
                    changes.Add(new AssetChangeEvent(AssetChangeKind.Removed, path));
            }
            _baseline = observed;
            return new ChangeSourceResult(changes);
        }
    }
}