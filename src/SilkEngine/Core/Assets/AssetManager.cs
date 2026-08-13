using System.Security.Cryptography;
using System.Text;

namespace SilkEngine.Core.Assets;

/// <summary>资产门面：同步/异步加载、缓存、帧末完成拾取（完整实现见任务 N.9）</summary>
public static class AssetManager
{
    /// <summary>
    /// 路径 → 稳定 GUID（归一化：反斜杠→斜杠、统一小写；跨运行与平台确定性）
    /// </summary>
    public static Guid PathToGuid(string path)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash);
    }
}
