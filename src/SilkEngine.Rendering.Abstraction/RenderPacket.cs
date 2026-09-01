using SilkEngine.Math;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>渲染参数值：受支持参数类型的判别联合（Float / Vector3；渲染契约，无资产语义）。</summary>
public readonly record struct RenderParameterValue
{
    /// <summary>参数值类型。</summary>
    public enum ValueKind
    {
        /// <summary>浮点标量</summary>
        Float,

        /// <summary>三维向量</summary>
        Vector3,
    }

    /// <summary>参数值类型。</summary>
    public ValueKind Kind { get; }

    private readonly float _float;
    private readonly Vector3 _vector3;

    private RenderParameterValue(ValueKind kind, float f, Vector3 v)
    {
        Kind = kind;
        _float = f;
        _vector3 = v;
    }

    /// <summary>创建 float 参数值。</summary>
    public static RenderParameterValue Float(float value) => new(ValueKind.Float, value, default);

    /// <summary>创建 Vector3 参数值。</summary>
    public static RenderParameterValue Vector3(Vector3 value) => new(ValueKind.Vector3, 0f, value);

    /// <summary>尝试读取 float 值；类型不匹配返回 false。</summary>
    public bool TryGetFloat(out float value)
    {
        value = _float;
        return Kind == ValueKind.Float;
    }

    /// <summary>尝试读取 Vector3 值；类型不匹配返回 false。</summary>
    public bool TryGetVector3(out Vector3 value)
    {
        value = _vector3;
        return Kind == ValueKind.Vector3;
    }

    /// <summary>浮点值（Float 参数读值用；Vector3 参数下为 0）。</summary>
    public float FloatValue => _float;
}

/// <summary>材质参数集合：构造时复制输入为私有字典，按名称读取参数值。</summary>
public sealed class RenderMaterialParameters(IEnumerable<(string Name, RenderParameterValue Value)> values)
{
    private readonly Dictionary<string, RenderParameterValue> _values = values.ToDictionary();

    /// <summary>读取 float 参数值；未定义或类型不匹配的参数名抛 <see cref="KeyNotFoundException"/>。</summary>
    public float GetFloat(string name)
    {
        if (!_values.TryGetValue(name, out var value) || !value.TryGetFloat(out var f))
            throw new KeyNotFoundException($"Material parameter '{name}' is not a float");
        return f;
    }

    /// <summary>读取 Vector3 参数值；未定义或类型不匹配的参数名抛 <see cref="KeyNotFoundException"/>。</summary>
    public Vector3 GetVector3(string name)
    {
        if (!_values.TryGetValue(name, out var value) || !value.TryGetVector3(out var v))
            throw new KeyNotFoundException($"Material parameter '{name}' is not a Vector3");
        return v;
    }

    /// <summary>按名称尝试读取参数值（类型不区分；未命中返回 false）。</summary>
    public bool TryGet(string name, out RenderParameterValue value) => _values.TryGetValue(name, out value);

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
