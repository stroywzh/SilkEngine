using System;
using System.Linq;
using Silk.NET.OpenGL;

namespace SilkEngine.Render.OpenGL;

public class OpenGLMesh : IMesh
{
    private readonly GL _gl;
    private readonly uint _vao,
        _vbo,
        _ebo;
    private readonly bool _hasIndices;
    private bool _disposed;

    public int VertexCount { get; }
    public int IndexCount { get; }
    public bool SupportsInstancing => true;

    public unsafe OpenGLMesh(GL gl, Mesh data)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);

        var vertices = data.Vertices;
        var layout = data.Layout;
        var stride = layout.Sum();

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* v = vertices)
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                v,
                BufferUsageARB.StaticDraw
            );

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

        _hasIndices = data.Indices != null && data.Indices.Length > 0;
        if (_hasIndices)
        {
            _ebo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            var indices = data.Indices!;
            fixed (int* idx = indices)
                gl.BufferData(
                    BufferTargetARB.ElementArrayBuffer,
                    (nuint)(indices.Length * sizeof(int)),
                    idx,
                    BufferUsageARB.StaticDraw
                );
            IndexCount = indices.Length;
        }
        else
        {
            _ebo = 0;
            IndexCount = 0;
        }

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        VertexCount = vertices.Length / stride;
    }

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        if (_hasIndices)
            _gl.DrawElements(
                PrimitiveType.Triangles,
                (uint)IndexCount,
                DrawElementsType.UnsignedInt,
                null
            );
        else
        {
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
        }
        _gl.BindVertexArray(0);
    }

    public unsafe void DrawInstanced(int instanceCount)
    {
        _gl.BindVertexArray(_vao);
        if (_hasIndices)
            _gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)IndexCount,
                DrawElementsType.UnsignedInt,
                null,
                (uint)instanceCount
            );
        else
            _gl.DrawArraysInstanced(
                PrimitiveType.Triangles,
                0,
                (uint)VertexCount,
                (uint)instanceCount
            );
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _gl.DeleteBuffer(_vbo);
        if (_ebo != 0)
        {
            _gl.DeleteBuffer(_ebo);
        }
        _gl.DeleteVertexArray(_vao);
        _disposed = true;
    }
}
