namespace SilkEngine.Host;

/// <summary>
/// 引擎组合根构建器：显式收集启动配置（后端、资产根、扩展注册），
/// <see cref="Build"/> 产出未初始化的 <see cref="EngineHost"/>。构造阶段不启动线程、不扫描 VFS。
/// </summary>
public sealed class EngineBuilder
{
    private readonly EngineOptions _options = new();

    internal EngineBuilder()
    {
    }

    /// <summary>使用 OpenGL 后端（当前唯一可用后端）。</summary>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseOpenGL()
    {
        _options.GraphicsBackend = GraphicsBackend.OpenGL;
        return this;
    }

    /// <summary>设置资产根目录。</summary>
    /// <param name="path">资产根（相对工作目录或绝对路径）。</param>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseAssetRoot(string path)
    {
        _options.AssetRoot = path;
        return this;
    }

    /// <summary>启用嵌入宿主循环模式（由宿主驱动帧而非内部 Run 循环）。</summary>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseEmbedded()
    {
        _options.Embedded = true;
        return this;
    }

    /// <summary>启用无头模式（测试专用；不打开真实窗口）。</summary>
    /// <returns>构建器自身（链式调用）。</returns>
    internal EngineBuilder UseHeadlessForTests()
    {
        _options.Headless = true;
        return this;
    }

    /// <summary>按当前配置产出未初始化的宿主。</summary>
    /// <returns>未初始化的 <see cref="EngineHost"/>。</returns>
    public EngineHost Build() => new(_options);
}