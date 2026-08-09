using System;
using System.Linq;
using Silk.NET.OpenGL;

namespace ProjectEngine.Render.OpenGL;

/// <summary>
/// IMesh 的 OpenGL 实现
/// <br/>在渲染线程将 Mesh 数据上传至 VAO/VBO
/// </summary>
public class OpenGLMesh : IMesh
{
    private readonly GL _gl;
    private readonly uint _vao,
        _vbo;
    private bool _disposed;

    /// <inheritdoc />
    public int VertexCount { get; }

    /// <inheritdoc />
    public bool SupportsInstancing => true;

    /// <summary>
    /// 从 Mesh 数据创建 VAO/VBO，上传顶点数据并配置顶点属性。
    /// </summary>
    public unsafe OpenGLMesh(GL gl, Mesh data)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var vertices = data.Vertices;
        var layout = data.Layout;
        var stride = layout.Sum();

        fixed (float* v = vertices)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                v,
                BufferUsageARB.StaticDraw
            );
        }

        int offset = 0;
        for (int i = 0; i < layout.Length; i++)
        {
            gl.VertexAttribPointer(
                (uint)i,
                layout[i],
                VertexAttribPointerType.Float,
                false,
                (uint)(stride * sizeof(float)),
                (void*)(offset * sizeof(float))
            );
            gl.EnableVertexAttribArray((uint)i);
            offset += layout[i];
        }

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        VertexCount = vertices.Length / stride;
    }

    /// <inheritdoc />
    public void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
        _gl.BindVertexArray(0);
    }

    /// <inheritdoc />
    public void DrawInstanced(int instanceCount)
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, (uint)VertexCount, (uint)instanceCount);
        _gl.BindVertexArray(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteVertexArray(_vao);
            _disposed = true;
        }
    }
}
