using System.Collections.Generic;
using ProjectEngine.Core;

namespace ProjectEngine.Render;

/// <summary>纯数据材质参数容器</summary>
public class Material
{
    /// <summary>材质标识名称</summary>
    public string Name { get; init; } = "";

    /// <summary>浮点类型 uniform 参数</summary>
    public Dictionary<string, float> Floats { get; } = new();

    /// <summary>Vector3 类型 uniform 参数</summary>
    public Dictionary<string, Vector3> Vectors { get; } = new();

    /// <summary>设置浮点 uniform 值</summary>
    public void SetFloat(string name, float value) => Floats[name] = value;

    /// <summary>设置 Vector3 uniform 值</summary>
    public void SetVector3(string name, Vector3 value) => Vectors[name] = value;
}
