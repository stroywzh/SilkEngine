using System;
using System.Collections.Generic;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render;

public interface IRenderBackend : IDisposable
{
    void InitWindow();
    void MakeContextCurrent();
    void ClearContext();
    void PumpWindowEvents();
    void ExecutePass(IReadOnlyList<DrawCommand> commands);
    void Present();
    IRenderBuffer CreateBuffer(int sizeBytes);
    void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount);
    bool ShouldClose { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>原生窗口对象（供 Input 等子系统绑定事件源），无窗口时返回 null</summary>
    Silk.NET.Windowing.IWindow? NativeWindow { get; }

    /// <summary>释放指定纹理的 GL 资源（渲染线程，帧首卸载队列处理）</summary>
    void ReleaseTexture(Texture2D texture);
}
