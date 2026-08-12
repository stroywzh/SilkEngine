using System;

namespace SilkEngine.Render;

/// <summary>
/// GPU 计算着色器
/// <br/>用于通用 GPU 计算，未来 GPU-Driven 渲染预留
/// </summary>
public interface IComputeShader : IDisposable
{
    /// <summary>
    /// 以指定工作组数量调度计算着色器
    /// </summary>
    void Dispatch(uint x, uint y, uint z);

    /// <summary>
    /// 为计算着色器设置缓冲区绑定
    /// </summary>
    void SetBuffer(string name, IntPtr bufferHandle);

    /// <summary>
    /// 计算着色器是否已成功编译
    /// </summary>
    bool IsCompiled { get; }
}
