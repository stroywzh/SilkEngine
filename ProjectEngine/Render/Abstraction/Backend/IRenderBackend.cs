using System;
using System.Collections.Generic;

namespace ProjectEngine.Render;

public interface IRenderBackend : IDisposable
{
    void InitWindow();
    void MakeContextCurrent();
    void ClearContext();
    void PumpWindowEvents();
    void ExecuteFrame(IReadOnlyList<DrawCommand> commands);
    IntPtr CreateBuffer(int sizeBytes);
    void DrawIndirect(IntPtr buffer, int offset, int drawCount);
    bool ShouldClose { get; }
    int Width { get; }
    int Height { get; }
}
