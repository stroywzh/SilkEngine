using System.Collections.Generic;
using System.Linq;
using SilkEngine.Math;

namespace SilkEngine.Render;

/// <summary>纯数据材质参数容器</summary>
public class Material
{
    /// <summary>材质标识名称</summary>
    public string Name { get; init; } = "";

    /// <summary>浮点类型 uniform 参数</summary>
    public Dictionary<string, float> Floats { get; } = new();

    /// <summary>Vector3 类型 uniform 参数</summary>
    public Dictionary<string, Vector3> Vectors { get; } = new();

    /// <summary>Matrix4x4 类型 uniform 参数</summary>
    public Dictionary<string, float[]> Matrices { get; } = new();

    /// <summary>设置浮点 uniform 值</summary>
    public void SetFloat(string name, float value) => Floats[name] = value;

    /// <summary>设置 Vector3 uniform 值</summary>
    public void SetVector3(string name, Vector3 value) => Vectors[name] = value;

    /// <summary>设置 Matrix4x4 uniform 值</summary>
    public void SetMatrix4x4(string name, Matrix4x4 value) =>
        Matrices[name] = [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];

    private int? _hash;

    public override int GetHashCode()
    {
        if (_hash == null)
        {
            var h = new HashCode();
            h.Add(Name);
            foreach (var kv in Floats) { h.Add(kv.Key); h.Add(kv.Value); }
            foreach (var kv in Vectors) { h.Add(kv.Key); h.Add(kv.Value.GetHashCode()); }
            h.Add(Matrices.Count);
            _hash = h.ToHashCode();
        }
        return _hash.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Material m) return false;
        if (Name != m.Name || Floats.Count != m.Floats.Count
            || Vectors.Count != m.Vectors.Count || Matrices.Count != m.Matrices.Count)
            return false;
        foreach (var kv in Floats)
            if (!m.Floats.TryGetValue(kv.Key, out var v) || kv.Value != v) return false;
        foreach (var kv in Vectors)
            if (!m.Vectors.TryGetValue(kv.Key, out var v) || kv.Value != v) return false;
        foreach (var kv in Matrices)
            if (!m.Matrices.TryGetValue(kv.Key, out var a) || kv.Value.Length != a.Length) return false;
        return true;
    }
}
