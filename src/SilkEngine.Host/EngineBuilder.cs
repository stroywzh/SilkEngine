using SilkEngine.Rendering.Backend;

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

    /// <summary>设置资产库根目录（AssetDB 存储；本阶段仅保存路径配置）。</summary>
    /// <param name="path">库根目录（相对工作目录或绝对路径）。</param>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseLibraryRoot(string path)
    {
        _options.LibraryRoot = path;
        return this;
    }

    /// <summary>设置 DXC（HLSL→SPIR-V）编译器可执行文件路径。</summary>
    /// <param name="path">dxc.exe 路径或所在目录；留空（直接略过）按 PATH 探测。</param>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseDxcPath(string path)
    {
        _options.DxcPath = path;
        return this;
    }

    /// <summary>启用嵌入宿主循环模式（由宿主驱动帧而非内部 Run 循环）。</summary>
    /// <returns>构建器自身（链式调用）。</returns>
    public EngineBuilder UseEmbedded()
    {
        _options.Embedded = true;
        return this;
    }

    /// <summary>注入外部着色器编译器（测试专用：替代真实 DXC 编译，仍走 OpenGL 后端 SPIR-V 加载路径）。</summary>
    /// <param name="compiler">着色器编译器实例</param>
    /// <returns>构建器自身（链式调用）。</returns>
    internal EngineBuilder UseShaderCompilerForTests(IShaderCompiler compiler)
    {
        _options.ShaderCompilerOverride = compiler;
        return this;
    }

    /// <summary>注入自定义渲染后端（测试专用：替代默认 Headless/OpenGL 选择）。</summary>
    /// <param name="backend">渲染后端实例</param>
    /// <returns>构建器自身（链式调用）。</returns>
    internal EngineBuilder UseRenderBackendForTests(IRenderBackend backend)
    {
        _options.BackendOverrideForTests = backend;
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