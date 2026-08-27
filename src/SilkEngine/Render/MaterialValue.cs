using System;
using System.Collections.Generic;
using SilkEngine.Math;

namespace SilkEngine.Render;

/// <summary>材质参数值：受支持参数类型的判别联合（Float / Vector3 / Matrix4x4）</summary>
public readonly struct MaterialValue
{
    /// <summary>参数值类型</summary>
    public enum ValueKind
    {
        /// <summary>浮点标量</summary>
        Float,

        /// <summary>三维向量</summary>
        Vector3,

        /// <summary>4x4 矩阵（行主序 16 个 float）</summary>
        Matrix4x4,
    }

    /// <summary>参数值类型</summary>
    public ValueKind Kind { get; }

    private readonly float _float;
    private readonly Vector3 _vector3;
    private readonly Matrix4x4 _matrix4x4;

    private MaterialValue(ValueKind kind, float f, Vector3 v, Matrix4x4 m)
    {
        Kind = kind;
        _float = f;
        _vector3 = v;
        _matrix4x4 = m;
    }

    /// <summary>创建浮点参数值</summary>
    /// <param name="value">浮点值</param>
    /// <returns>Float 类型参数值</returns>
    public static MaterialValue Float(float value) => new(ValueKind.Float, value, default, default);

    /// <summary>创建 Vector3 参数值</summary>
    /// <param name="value">三维向量</param>
    /// <returns>Vector3 类型参数值</returns>
    public static MaterialValue Vector3(Vector3 value) => new(ValueKind.Vector3, 0f, value, default);

    /// <summary>创建 Matrix4x4 参数值</summary>
    /// <param name="value">矩阵（按行主序展开为 16 个连续 float）</param>
    /// <returns>Matrix4x4 类型参数值</returns>
    public static MaterialValue Matrix4x4(Matrix4x4 value) => new(ValueKind.Matrix4x4, 0f, default, value);

    /// <summary>尝试读取浮点值；类型不匹配返回 false</summary>
    /// <param name="value">浮点值（类型不匹配时为默认值）</param>
    /// <returns>是否为 Float 类型</returns>
    public bool TryGetFloat(out float value)
    {
        value = _float;
        return Kind == ValueKind.Float;
    }

    /// <summary>尝试读取 Vector3 值；类型不匹配返回 false</summary>
    /// <param name="value">三维向量（类型不匹配时为默认值）</param>
    /// <returns>是否为 Vector3 类型</returns>
    public bool TryGetVector3(out Vector3 value)
    {
        value = _vector3;
        return Kind == ValueKind.Vector3;
    }

    /// <summary>尝试读取 Matrix4x4 值；类型不匹配返回 false</summary>
    /// <param name="value">矩阵（类型不匹配时为默认值）</param>
    /// <returns>是否为 Matrix4x4 类型</returns>
    public bool TryGetMatrix4x4(out Matrix4x4 value)
    {
        value = _matrix4x4;
        return Kind == ValueKind.Matrix4x4;
    }
}

/// <summary>材质参数只读快照：构造时复制输入，此后不可变（供渲染层绑定消费，避免观察到并发修改）</summary>
public sealed class MaterialParameterSnapshot
{
    private readonly Dictionary<string, MaterialValue> _values;

    /// <summary>从参数集合复制创建快照；同名参数后者覆盖前者</summary>
    /// <param name="parameters">参数名称与值集合</param>
    public MaterialParameterSnapshot(IEnumerable<(string Name, MaterialValue Value)> parameters)
    {
        _values = new Dictionary<string, MaterialValue>();
        foreach (var (name, value) in parameters)
            _values[name] = value;
    }

    /// <summary>读取浮点参数；未命中或类型不匹配抛 <see cref="KeyNotFoundException"/></summary>
    /// <param name="name">参数名称</param>
    /// <returns>浮点值</returns>
    public float GetFloat(string name)
    {
        if (!TryGetFloat(name, out var value))
            throw new KeyNotFoundException($"Material parameter '{name}' is not a float");
        return value;
    }

    /// <summary>读取 Vector3 参数；未命中或类型不匹配抛 <see cref="KeyNotFoundException"/></summary>
    /// <param name="name">参数名称</param>
    /// <returns>三维向量</returns>
    public Vector3 GetVector3(string name)
    {
        if (!TryGetVector3(name, out var value))
            throw new KeyNotFoundException($"Material parameter '{name}' is not a Vector3");
        return value;
    }

    /// <summary>读取 Matrix4x4 参数；未命中或类型不匹配抛 <see cref="KeyNotFoundException"/></summary>
    /// <param name="name">参数名称</param>
    /// <returns>矩阵</returns>
    public Matrix4x4 GetMatrix4x4(string name)
    {
        if (!TryGetMatrix4x4(name, out var value))
            throw new KeyNotFoundException($"Material parameter '{name}' is not a Matrix4x4");
        return value;
    }

    /// <summary>按名称尝试读取参数值</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">参数值（未命中时为默认值）</param>
    /// <returns>是否命中</returns>
    public bool TryGet(string name, out MaterialValue value) => _values.TryGetValue(name, out value);

    /// <summary>按名称尝试读取浮点参数；类型不匹配视为未命中</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">浮点值（未命中或类型不匹配时为默认值）</param>
    /// <returns>是否命中且为 Float 类型</returns>
    public bool TryGetFloat(string name, out float value)
    {
        if (_values.TryGetValue(name, out var v))
            return v.TryGetFloat(out value);
        value = default;
        return false;
    }

    /// <summary>按名称尝试读取 Vector3 参数；类型不匹配视为未命中</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">三维向量（未命中或类型不匹配时为默认值）</param>
    /// <returns>是否命中且为 Vector3 类型</returns>
    public bool TryGetVector3(string name, out Vector3 value)
    {
        if (_values.TryGetValue(name, out var v))
            return v.TryGetVector3(out value);
        value = default;
        return false;
    }

    /// <summary>按名称尝试读取 Matrix4x4 参数；类型不匹配视为未命中</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">矩阵（未命中或类型不匹配时为默认值）</param>
    /// <returns>是否命中且为 Matrix4x4 类型</returns>
    public bool TryGetMatrix4x4(string name, out Matrix4x4 value)
    {
        if (_values.TryGetValue(name, out var v))
            return v.TryGetMatrix4x4(out value);
        value = default;
        return false;
    }

    /// <summary>快照参数数量</summary>
    public int Count => _values.Count;
}
