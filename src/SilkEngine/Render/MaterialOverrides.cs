using System;
using System.Collections.Generic;
using SilkEngine.Math;

namespace SilkEngine.Render;

/// <summary>材质运行时覆盖参数集：同名参数跨类型互斥（单键单值，后设覆盖先设）；每次变更 Version 单调递增</summary>
public sealed class MaterialOverrides
{
    private readonly Dictionary<string, MaterialValue> _values = new();

    /// <summary>变更版本号：每次 Set*/Clear* 递增，供渲染层检测覆盖参数变化</summary>
    public int Version { get; private set; }

    /// <summary>当前覆盖参数数量</summary>
    public int Count => _values.Count;

    /// <summary>设置浮点覆盖参数（同名其它类型参数被移除）</summary>
    /// <param name="name">参数名称，不得为空或空白</param>
    /// <param name="value">浮点值</param>
    public void SetFloat(string name, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = MaterialValue.Float(value);
        Version++;
    }

    /// <summary>设置 Vector3 覆盖参数（同名其它类型参数被移除）</summary>
    /// <param name="name">参数名称，不得为空或空白</param>
    /// <param name="value">三维向量</param>
    public void SetVector3(string name, Vector3 value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = MaterialValue.Vector3(value);
        Version++;
    }

    /// <summary>设置 Matrix4x4 覆盖参数（同名其它类型参数被移除；按行主序展开为 16 个连续 float）</summary>
    /// <param name="name">参数名称，不得为空或空白</param>
    /// <param name="value">矩阵值</param>
    public void SetMatrix4x4(string name, Matrix4x4 value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = MaterialValue.Matrix4x4(value);
        Version++;
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

    /// <summary>创建当前覆盖参数的只读快照（复制数据，之后互不影响）</summary>
    /// <returns>参数只读快照</returns>
    public MaterialParameterSnapshot Snapshot()
    {
        var entries = new (string Name, MaterialValue Value)[_values.Count];
        int i = 0;
        foreach (var kv in _values)
            entries[i++] = (kv.Key, kv.Value);
        return new MaterialParameterSnapshot(entries);
    }

    /// <summary>清除全部覆盖参数（Version 递增）</summary>
    public void ClearOverrides()
    {
        _values.Clear();
        Version++;
    }

    /// <summary>清除全部覆盖参数（Version 递增；ClearOverrides 的简写）</summary>
    public void Clear() => ClearOverrides();
}
