using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SilkEngine.Assets.Database;

/// <summary>
/// 基于 SQLite 的 <see cref="IAssetDatabase"/> 实现：单连接 + 事务写入、WAL 模式；
/// 初始化时检测损坏文件，改名备份后抛 <see cref="AssetDatabaseCorruptException"/>，不删除源文件。
/// </summary>
internal sealed class SqliteAssetDatabase : IAssetDatabase
{
    // SQLITE_CORRUPT
    private const int SqliteCorruptErrorCode = 11;

    // SQLITE_NOTADB
    private const int SqliteNotADatabaseErrorCode = 26;

    private readonly string _databasePath;
    private readonly SqliteConnection _connection;

    private static readonly string[] SchemaStatements =
    [
        """CREATE TABLE IF NOT EXISTS SchemaMigrations (Version INTEGER PRIMARY KEY, AppliedAtUtc TEXT NOT NULL);""",
        """CREATE TABLE IF NOT EXISTS FileNodes (NodeId TEXT PRIMARY KEY, LogicalPath TEXT NOT NULL UNIQUE);""",
        """CREATE TABLE IF NOT EXISTS Assets (AssetId TEXT PRIMARY KEY, LogicalPath TEXT NOT NULL UNIQUE, AssetType TEXT NOT NULL, SourceFingerprint TEXT NOT NULL, SourceRevision INTEGER NOT NULL);""",
        """CREATE TABLE IF NOT EXISTS Dependencies (AssetId TEXT NOT NULL REFERENCES Assets(AssetId) ON DELETE CASCADE, DependsOnPath TEXT NOT NULL, PRIMARY KEY (AssetId, DependsOnPath));""",
        """CREATE TABLE IF NOT EXISTS Builds (BuildKey TEXT PRIMARY KEY, AssetId TEXT NOT NULL REFERENCES Assets(AssetId) ON DELETE CASCADE, CachePath TEXT NOT NULL, SourceFingerprint TEXT NOT NULL);""",
    ];

    /// <summary>创建指向指定数据库文件的资产数据库</summary>
    /// <param name="databasePath">SQLite 数据库文件路径</param>
    public SqliteAssetDatabase(string databasePath)
    {
        _databasePath = databasePath;
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString());
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
            }

            await _connection.ExecuteAsync(new CommandDefinition("PRAGMA journal_mode=WAL;", cancellationToken: cancellationToken));
            await _connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys=ON;", cancellationToken: cancellationToken));

            foreach (var statement in SchemaStatements)
            {
                await _connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
            }

            await _connection.ExecuteAsync(new CommandDefinition(
                "INSERT OR IGNORE INTO SchemaMigrations (Version, AppliedAtUtc) VALUES (1, @AppliedAtUtc);",
                new { AppliedAtUtc = DateTime.UtcNow.ToString("o") },
                cancellationToken: cancellationToken));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteCorruptErrorCode or SqliteNotADatabaseErrorCode)
        {
            var backupPath = BackupCorruptDatabase();
            throw new AssetDatabaseCorruptException(_databasePath, backupPath, ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AssetDbAssetRecord?> GetAssetAsync(string logicalPath, CancellationToken cancellationToken)
    {
        var row = await _connection.QuerySingleOrDefaultAsync<AssetRow>(new CommandDefinition(
            "SELECT AssetId, LogicalPath, AssetType, SourceFingerprint, SourceRevision FROM Assets WHERE LogicalPath = @LogicalPath;",
            new { LogicalPath = logicalPath },
            cancellationToken: cancellationToken));
        return row is null ? null : MapAsset(row);
    }

    /// <inheritdoc/>
    public async ValueTask<AssetDbBuildRecord?> GetBuildAsync(string buildKey, CancellationToken cancellationToken)
    {
        var row = await _connection.QuerySingleOrDefaultAsync<BuildRow>(new CommandDefinition(
            "SELECT AssetId, BuildKey, CachePath, SourceFingerprint FROM Builds WHERE BuildKey = @BuildKey;",
            new { BuildKey = buildKey },
            cancellationToken: cancellationToken));
        return row is null ? null : MapBuild(row);
    }

    /// <inheritdoc/>
    public async ValueTask UpsertAssetAsync(AssetDbAssetRecord record, CancellationToken cancellationToken)
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        await _connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO Assets (AssetId, LogicalPath, AssetType, SourceFingerprint, SourceRevision)
            VALUES (@AssetId, @LogicalPath, @AssetType, @SourceFingerprint, @SourceRevision)
            ON CONFLICT(AssetId) DO UPDATE SET
                LogicalPath = excluded.LogicalPath,
                AssetType = excluded.AssetType,
                SourceFingerprint = excluded.SourceFingerprint,
                SourceRevision = excluded.SourceRevision;
            """,
            new
            {
                AssetId = record.AssetId.Value.ToString(),
                record.LogicalPath,
                AssetType = record.AssetType.Value,
                record.SourceFingerprint,
                SourceRevision = (long)record.SourceRevision,
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask UpsertBuildAsync(AssetDbBuildRecord record, CancellationToken cancellationToken)
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        await _connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO Builds (BuildKey, AssetId, CachePath, SourceFingerprint)
            VALUES (@BuildKey, @AssetId, @CachePath, @SourceFingerprint)
            ON CONFLICT(BuildKey) DO UPDATE SET
                AssetId = excluded.AssetId,
                CachePath = excluded.CachePath,
                SourceFingerprint = excluded.SourceFingerprint;
            """,
            new
            {
                record.BuildKey,
                AssetId = record.AssetId.Value.ToString(),
                record.CachePath,
                record.SourceFingerprint,
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask ReconcileAsync(AssetDbFileNodeRecord fileNode, AssetDbAssetRecord asset, CancellationToken cancellationToken)
    {
        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        await _connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FileNodes WHERE LogicalPath = @LogicalPath AND NodeId <> @NodeId;",
            new { NodeId = fileNode.NodeId.Value.ToString(), fileNode.LogicalPath },
            transaction,
            cancellationToken: cancellationToken));
        await _connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO FileNodes (NodeId, LogicalPath) VALUES (@NodeId, @LogicalPath)
            ON CONFLICT(NodeId) DO UPDATE SET LogicalPath = excluded.LogicalPath;
            """,
            new { NodeId = fileNode.NodeId.Value.ToString(), fileNode.LogicalPath },
            transaction,
            cancellationToken: cancellationToken));

        await _connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Assets WHERE LogicalPath = @LogicalPath AND AssetId <> @AssetId;",
            new { AssetId = asset.AssetId.Value.ToString(), asset.LogicalPath },
            transaction,
            cancellationToken: cancellationToken));
        await _connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO Assets (AssetId, LogicalPath, AssetType, SourceFingerprint, SourceRevision)
            VALUES (@AssetId, @LogicalPath, @AssetType, @SourceFingerprint, @SourceRevision)
            ON CONFLICT(AssetId) DO UPDATE SET
                LogicalPath = excluded.LogicalPath,
                AssetType = excluded.AssetType,
                SourceFingerprint = excluded.SourceFingerprint,
                SourceRevision = excluded.SourceRevision;
            """,
            new
            {
                AssetId = asset.AssetId.Value.ToString(),
                asset.LogicalPath,
                AssetType = asset.AssetType.Value,
                asset.SourceFingerprint,
                SourceRevision = (long)asset.SourceRevision,
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<AssetDatabaseSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        var assetRows = await _connection.QueryAsync<AssetRow>(new CommandDefinition(
            "SELECT AssetId, LogicalPath, AssetType, SourceFingerprint, SourceRevision FROM Assets;",
            cancellationToken: cancellationToken));
        var fileNodeRows = await _connection.QueryAsync<FileNodeRow>(new CommandDefinition(
            "SELECT NodeId, LogicalPath FROM FileNodes;",
            cancellationToken: cancellationToken));
        var dependencyRows = await _connection.QueryAsync<DependencyRow>(new CommandDefinition(
            "SELECT AssetId, DependsOnPath FROM Dependencies;",
            cancellationToken: cancellationToken));
        var buildRows = await _connection.QueryAsync<BuildRow>(new CommandDefinition(
            "SELECT AssetId, BuildKey, CachePath, SourceFingerprint FROM Builds;",
            cancellationToken: cancellationToken));

        return new AssetDatabaseSnapshot(
            [.. assetRows.Select(MapAsset)],
            [.. fileNodeRows.Select(row => new AssetDbFileNodeRecord(new VirtualNodeId(Guid.Parse(row.NodeId)), row.LogicalPath))],
            [.. dependencyRows.Select(row => new AssetDbDependencyRecord(new AssetId(Guid.Parse(row.AssetId)), row.DependsOnPath))],
            [.. buildRows.Select(MapBuild)]);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private string BackupCorruptDatabase()
    {
        _connection.Close();
        var backupPath = $"{_databasePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(_databasePath, backupPath);
        return backupPath;
    }

    private static AssetDbAssetRecord MapAsset(AssetRow row) => new(
        new AssetId(Guid.Parse(row.AssetId)),
        row.LogicalPath,
        new AssetTypeId(row.AssetType),
        row.SourceFingerprint,
        (ulong)row.SourceRevision);

    private static AssetDbBuildRecord MapBuild(BuildRow row) => new(
        new AssetId(Guid.Parse(row.AssetId)),
        row.BuildKey,
        row.CachePath,
        row.SourceFingerprint);

    private sealed class AssetRow
    {
        public string AssetId { get; init; } = string.Empty;
        public string LogicalPath { get; init; } = string.Empty;
        public string AssetType { get; init; } = string.Empty;
        public string SourceFingerprint { get; init; } = string.Empty;
        public long SourceRevision { get; init; }
    }

    private sealed class BuildRow
    {
        public string AssetId { get; init; } = string.Empty;
        public string BuildKey { get; init; } = string.Empty;
        public string CachePath { get; init; } = string.Empty;
        public string SourceFingerprint { get; init; } = string.Empty;
    }

    private sealed class FileNodeRow
    {
        public string NodeId { get; init; } = string.Empty;
        public string LogicalPath { get; init; } = string.Empty;
    }

    private sealed class DependencyRow
    {
        public string AssetId { get; init; } = string.Empty;
        public string DependsOnPath { get; init; } = string.Empty;
    }
}
