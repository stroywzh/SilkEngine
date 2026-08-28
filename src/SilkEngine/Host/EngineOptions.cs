using System;

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

    /// <summary>无头模式：不打开真实窗口，供测试装配使用。</summary>
    public bool Headless { get; internal set; }

    /// <summary>图形后端。</summary>
    public GraphicsBackend GraphicsBackend { get; internal set; } = GraphicsBackend.OpenGL;

    /// <summary>是否嵌入宿主循环（由宿主驱动帧而非内部 Run 循环）。</summary>
    public bool Embedded { get; internal set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssetRoot))
            throw new ArgumentException("AssetRoot 不能为空白", nameof(AssetRoot));
        if(!Directory.Exists(AssetRoot))
        {
            SilkEngine.Core.Log.Warning($"AssetRoot {AssetRoot} 不存在");

        }
    }
}