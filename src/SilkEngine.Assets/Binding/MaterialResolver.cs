using System;
using System.Collections.Generic;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Assets.Binding;

/// <summary>
/// 材质绑定解析（Assets 域）：将运行时材质实例（默认参数快照 + 实例覆盖参数）合并解析为
/// 无资产语义的 <see cref="RenderMaterialParameters"/>，供 Rendering 域消费。
/// Rendering 不读取 MaterialAsset 或 MaterialBinding；本解析器是两域的边界转换。
/// </summary>
public static class MaterialResolver
{
    /// <summary>
    /// 解析材质实例为渲染参数：默认参数（可选）为基底，实例覆盖参数优先（同名覆盖）。
    /// 不支持的参数值类型（如 Matrix4x4）跳过；空参数集返回空参数集合。
    /// </summary>
    /// <param name="material">运行时材质实例</param>
    /// <param name="defaults">源材质默认参数快照（可为 null，此时仅使用覆盖参数）</param>
    /// <returns>无资产语义的渲染参数集合</returns>
    public static RenderMaterialParameters ResolveForRender(Material material, MaterialParameterSnapshot? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        var merged = new Dictionary<string, MaterialValue>();
        if (defaults is not null)
            foreach (var (name, value) in defaults)
                merged[name] = value;
        foreach (var (name, value) in material.Overrides.Snapshot())
            merged[name] = value;

        var entries = new List<(string Name, RenderParameterValue Value)>(merged.Count);
        foreach (var (name, value) in merged)
            if (TryConvert(value, out var renderValue))
                entries.Add((name, renderValue));
        return new RenderMaterialParameters(entries);
    }

    /// <summary>判断材质参数值是否可转换为渲染参数值（仅 Float/Vector3；Matrix4x4/Texture 不支持）。</summary>
    /// <param name="value">材质参数值</param>
    /// <returns>可转换为渲染参数值为 true</returns>
    public static bool IsConvertibleToRenderValue(MaterialValue value)
        => value.TryGetFloat(out _) || value.TryGetVector3(out _);

    private static bool TryConvert(MaterialValue value, out RenderParameterValue result)
    {
        if (value.TryGetFloat(out var f))
        {
            result = RenderParameterValue.Float(f);
            return true;
        }
        if (value.TryGetVector3(out var v))
        {
            result = RenderParameterValue.Vector3(v);
            return true;
        }
        result = default;
        return false;
    }
}
