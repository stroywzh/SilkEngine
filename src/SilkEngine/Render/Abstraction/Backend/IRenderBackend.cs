using System;
using System.Collections.Generic;

namespace SilkEngine.Render;

public interface IRenderBackend : IDisposable
{
    void InitWindow();
    void MakeContextCurrent();
    void ClearContext();
    void PumpWindowEvents();
    void ExecuteFrame(IReadOnlyList<DrawCommand> commands);
    void ExecutePass(IReadOnlyList<DrawCommand> commands);
    void Present();
    IntPtr CreateBuffer(int sizeBytes);
    void DrawIndirect(IntPtr buffer, int offset, int drawCount);
    bool ShouldClose { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>原生窗口对象（供 Input 等子系统绑定事件源），无窗口时返回 null</summary>
    Silk.NET.Windowing.IWindow? NativeWindow { get; }
}
