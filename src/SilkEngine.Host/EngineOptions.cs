using System;
using SilkEngine.Rendering.Backend;

namespace SilkEngine.Host;

/// <summary>图形后端选择（当前仅 OpenGL；Vulkan 为未来扩展点）。</summary>
public enum GraphicsBackend
{
    OpenGL,
}

/// <summary>
/// 引擎启动配置（窗口、资产根、输入与后端）。由 <see cref="EngineBuilder"/> 经 internal setter 装配，
/// 业务代码只读。
/// </summary>
public sealed class EngineOptions
{
    /// <summary>资产根目录（相对工作目录或绝对路径）。</summary>
    public string AssetRoot { get; internal set; } = "Assets";

    /// <summary>资产库根目录（AssetDB 存储位置；本阶段仅保存路径配置，接线由后续任务完成）。</summary>
    public string LibraryRoot { get; internal set; } = "Library";

    /// <summary>无头模式：不打开真实窗口，供测试装配使用。</summary>
    public bool Headless { get; internal set; }

    /// <summary>图形后端。</summary>
    public GraphicsBackend GraphicsBackend { get; internal set; } = GraphicsBackend.OpenGL;

    /// <summary>是否嵌入宿主循环（由宿主驱动帧而非内部 Run 循环）。</summary>
    public bool Embedded { get; internal set; }

    /// <summary>DXC（HLSL→SPIR-V）编译器可执行文件路径（文件或所在目录；留空按 PATH 探测）。</summary>
    public string? DxcPath { get; internal set; }

    /// <summary>测试注入的着色器编译器（替代真实 DXC；仅 EngineBuilder 内部装配使用）。</summary>
    internal IShaderCompiler? ShaderCompilerOverride { get; set; }

    /// <summary>测试注入的渲染后端（替代默认 Headless/OpenGL；仅 EngineBuilder 内部装配使用）。</summary>
    internal IRenderBackend? BackendOverrideForTests { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssetRoot))
            throw new ArgumentException("AssetRoot 不能为空白", nameof(AssetRoot));
        if (string.IsNullOrWhiteSpace(LibraryRoot))
            throw new ArgumentException("LibraryRoot 不能为空白", nameof(LibraryRoot));
        if (!Directory.Exists(AssetRoot))
        {
            SilkEngine.Core.Log.Warning($"AssetRoot {AssetRoot} 不存在");
        }

        // DxcPath 校验（现有风格：非致命问题以告警提示）：不存在或相对路径越出工作区根时降级为 PATH 探测
        if (!string.IsNullOrWhiteSpace(DxcPath))
        {
            if (!File.Exists(DxcPath) && !Directory.Exists(DxcPath))
            {
                SilkEngine.Core.Log.Warning($"DxcPath {DxcPath} 不存在（将按 PATH 探测 dxc.exe）");
            }
            else if (!Path.IsPathRooted(DxcPath) && EscapesWorkspaceRoot(DxcPath))
            {
                SilkEngine.Core.Log.Warning($"DxcPath {DxcPath} 为相对路径且越出工作区根，将忽略该配置（改按 PATH 探测）");
            }
        }
    }

    /// <summary>判定相对路径是否越出工作区根（绝对路径视为外部工具合法路径，不判定越界）。</summary>
    private static bool EscapesWorkspaceRoot(string path)
    {
        var root = Path.GetFullPath(".");
        var full = Path.GetFullPath(path);
        return !string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
            && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}