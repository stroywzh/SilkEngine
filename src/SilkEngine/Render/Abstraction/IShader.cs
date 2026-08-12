using System;

namespace SilkEngine.Render;

/// <summary>
/// 已编译的 GPU 着色器程序
/// <br/>由后端从 Shader 数据创建
/// </summary>
public interface IShader : IDisposable
{
    /// <summary>
    /// 激活着色器程序
    /// </summary>
    void Use();

    /// <summary>
    /// 着色器是否已成功编译
    /// </summary>
    bool IsCompiled { get; }
}
