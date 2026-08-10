using System;
using System.Collections.Generic;

namespace ProjectEngine.Render;

/// <summary>用于单元测试的无 GPU 渲染后端。记录执行帧时收到的命令并跟踪帧执行计数。</summary>
public class StubRenderBackend : IRenderBackend
{
    private bool _disposed;
    private int _bufferCounter = 1;

    /// <summary>已执行的帧总数。</summary>
    public int ExecuteFrameCount { get; private set; }

    /// <summary>所有已执行的命令批次。</summary>
    public List<IReadOnlyList<DrawCommand>> SubmittedCommandBatches { get; } = new();

    /// <summary>窗口是否已请求关闭。</summary>
    public bool ShouldClose { get; set; }

    /// <summary>当前帧缓冲区宽（像素）。</summary>
    public int Width { get; private set; } = 800;

    /// <summary>当前帧缓冲区高（像素）。</summary>
    public int Height { get; private set; } = 600;

    /// <inheritdoc />
    public void InitWindow() { }

    /// <inheritdoc />
    public void MakeContextCurrent() { }

    /// <inheritdoc />
    public void ClearContext() { }

    /// <inheritdoc />
    public void PumpWindowEvents() { }

    /// <inheritdoc />
    public void ExecuteFrame(IReadOnlyList<DrawCommand> commands)
    {
        SubmittedCommandBatches.Add(commands);
        ExecuteFrameCount++;
    }

    /// <inheritdoc />
    public IntPtr CreateBuffer(int sizeBytes) => (IntPtr)(_bufferCounter++);

    /// <inheritdoc />
    public void DrawIndirect(IntPtr buffer, int offset, int drawCount) { }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
