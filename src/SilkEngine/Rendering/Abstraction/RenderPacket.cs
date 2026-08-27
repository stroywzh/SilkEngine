using SilkEngine.Math;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>渲染参数值；当前仅支持 float 标量。</summary>
public readonly record struct RenderParameterValue(float FloatValue)
{
    /// <summary>创建 float 参数值。</summary>
    public static RenderParameterValue Float(float value) => new(value);
}

/// <summary>材质参数集合：构造时复制输入为私有字典，按名称读取参数值。</summary>
public sealed class RenderMaterialParameters(IEnumerable<(string Name, RenderParameterValue Value)> values)
{
    private readonly Dictionary<string, RenderParameterValue> _values = values.ToDictionary();

    /// <summary>读取 float 参数值；未定义的参数名抛 <see cref="KeyNotFoundException"/>。</summary>
    public float GetFloat(string name) => _values[name].FloatValue;

    /// <summary>枚举全部参数（名称 + 值；渲染后端上传用，惰性投影零分配）。</summary>
    public IEnumerable<(string Name, RenderParameterValue Value)> Enumerate()
    {
        foreach (var (name, value) in _values)
            yield return (name, value);
    }
}

/// <summary>单次渲染提交的不可变数据：仅引用 GPU 句柄、材质参数与模型矩阵，无任何资产身份。</summary>
public sealed record RenderPacket(
    RenderShaderHandle Shader,
    RenderMeshHandle Mesh,
    RenderTextureHandle Texture,
    RenderMaterialParameters Material,
    Matrix4x4 ModelMatrix);
