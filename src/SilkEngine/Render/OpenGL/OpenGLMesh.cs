using System;
using System.Linq;
using Silk.NET.OpenGL;

namespace SilkEngine.Render.OpenGL;

/// <summary>
/// IMesh 的 OpenGL 实现：在渲染线程从 Mesh 数据创建 VAO/VBO/EBO，支持索引与非索引绘制
/// </summary>
public class OpenGLMesh : IMesh
{
    private readonly GL _gl;
    private readonly uint _vao,
        _vbo,
        _ebo;
    private readonly bool _hasIndices;
    private bool _disposed;

    /// <summary>顶点数（DrawArrays 用）</summary>
    public int VertexCount { get; }

    /// <summary>索引数（非索引绘制时为 0）</summary>
    public int IndexCount { get; }

    /// <summary>是否支持 GPU 实例化（当前恒为 true）</summary>
    public bool SupportsInstancing => true;

    /// <summary>
    /// 从 Mesh 数据创建 VAO/VBO（+ 可选 EBO），按 Layout 配置顶点属性；渲染线程上下文内调用
    /// </summary>
    /// <param name="gl">OpenGL API 实例</param>
    /// <param name="data">网格数据</param>
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

    /// <summary>绑定 VAO 执行绘制（有索引走 DrawElements，否则 DrawArrays）</summary>
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

    /// <summary>一次 GPU 调用绘制 instanceCount 个实例</summary>
    /// <param name="instanceCount">实例数量</param>
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

    /// <summary>释放 VAO/VBO/EBO（幂等）</summary>
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
