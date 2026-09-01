namespace SilkEngine.Assets.Database;

/// <summary>资产数据库操作异常基类：数据库族错误的统一类型，便于上层按域捕获</summary>
public class AssetDatabaseException : Exception
{
    /// <summary>创建资产数据库异常</summary>
    /// <param name="message">错误描述</param>
    /// <param name="innerException">引发本异常的底层异常</param>
    public AssetDatabaseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>资产数据库损坏异常：初始化时检测到 SQLITE_CORRUPT/SQLITE_NOTADB，损坏文件已改名备份（未删除）</summary>
public sealed class AssetDatabaseCorruptException : AssetDatabaseException
{
    /// <summary>损坏的数据库文件路径</summary>
    public string DatabasePath { get; }

    /// <summary>损坏文件改名后的备份路径（保留在磁盘上供人工排查）</summary>
    public string BackupPath { get; }

    /// <summary>创建资产数据库损坏异常</summary>
    /// <param name="databasePath">损坏的数据库文件路径</param>
    /// <param name="backupPath">已生成的备份文件路径</param>
    /// <param name="innerException">底层 SqliteException</param>
    public AssetDatabaseCorruptException(string databasePath, string backupPath, Exception? innerException = null)
        : base($"资产数据库损坏，已备份至 {backupPath}：{databasePath}", innerException)
    {
        DatabasePath = databasePath;
        BackupPath = backupPath;
    }
}
