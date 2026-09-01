namespace SilkEngine.Tests.Assets;

/// <summary>
/// 资产工作流测试共享支持：可复现的临时目录创建与递归清理，供测试在 finally/Dispose 中使用。
/// </summary>
internal static class TestTempDirectory
{
    /// <summary>在系统临时目录下创建唯一的空子目录</summary>
    /// <returns>新目录的完整路径</returns>
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "silk-asset-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>递归删除目录及其全部内容；目录不存在时忽略</summary>
    /// <param name="path">待删除的目录路径</param>
    public static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
