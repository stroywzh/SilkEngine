using SilkEngine.Math;

namespace SilkEngine.Render;

/// <summary>材质运行时实例：由源材质资产（<see cref="MaterialReference"/>）派生，携带实例私有覆盖参数</summary>
public sealed class Material
{
    /// <summary>源材质资产引用（多个实例可共享同一来源）</summary>
    public MaterialReference Source { get; }

    /// <summary>运行时覆盖参数（实例私有：同源实例互不影响）</summary>
    public MaterialOverrides Overrides { get; } = new();

    /// <summary>创建材质运行时实例</summary>
    /// <param name="source">源材质资产引用</param>
    public Material(MaterialReference source) => Source = source;

    /// <summary>设置浮点覆盖参数</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">浮点值</param>
    public void SetFloat(string name, float value) => Overrides.SetFloat(name, value);

    /// <summary>设置 Vector3 覆盖参数</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">三维向量</param>
    public void SetVector3(string name, Vector3 value) => Overrides.SetVector3(name, value);

    /// <summary>设置 Matrix4x4 覆盖参数</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">矩阵值</param>
    public void SetMatrix4x4(string name, Matrix4x4 value) => Overrides.SetMatrix4x4(name, value);
}
