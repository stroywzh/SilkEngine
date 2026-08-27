using System.Collections.Generic;
using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Math;

namespace SilkEngine.Render;

// 临时兼容类型：旧资产对象模型（IAsset 材质），Material Instance 任务 4 删除。
// 仅将原 Material 类更名，行为与 API 完全不变（Name/Floats/Vectors/Matrices/MainTexture/SetFloat/SetVector3/SetMatrix4x4/Equals/GetHashCode/MaterialDisposed）。

/// <summary>纯数据材质参数容器（旧资产对象模型）</summary>
public class MaterialLegacy : IAsset, IReleaseAwareAsset
{
    /// <summary>材质标识名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>浮点类型 uniform 参数</summary>
    public Dictionary<string, float> Floats { get; } = new();

    /// <summary>Vector3 类型 uniform 参数</summary>
    public Dictionary<string, Vector3> Vectors { get; } = new();

    /// <summary>Matrix4x4 类型 uniform 参数</summary>
    public Dictionary<string, float[]> Matrices { get; } = new();

    private Texture2D? _mainTexture;

    /// <summary>主纹理槽（引用计数由 AssetManager 托管：赋值 +1、替换/清空 -1）</summary>
    public Texture2D? MainTexture
    {
        get => _mainTexture;
        set => AssetManager.SetTrackedAmbient(ref _mainTexture, value);
    }

    public MaterialLegacy()
    {
        // 材质释放（计数归零）→ 级联归还主纹理引用；释放方管理器实例由 AssetManager.TryRelease 传入
        MaterialDisposed += manager => manager.TryRelease(_mainTexture);
    }

    /// <summary>设置浮点 uniform 值（同名键从其它字典互斥移除）</summary>
    /// <param name="name">uniform 名称</param>
    /// <param name="value">浮点值</param>
    public void SetFloat(string name, float value)
    {
        Vectors.Remove(name);
        Matrices.Remove(name);
        Floats[name] = value;
    }

    /// <summary>设置 Vector3 uniform 值（同名键从其它字典互斥移除）</summary>
    /// <param name="name">uniform 名称</param>
    /// <param name="value">Vector3 值</param>
    public void SetVector3(string name, Vector3 value)
    {
        Floats.Remove(name);
        Matrices.Remove(name);
        Vectors[name] = value;
    }

    /// <summary>设置 Matrix4x4 uniform 值（同名键从其它字典互斥移除）</summary>
    /// <param name="name">uniform 名称</param>
    /// <param name="value">矩阵值（按行主序展开为 16 个连续 float）</param>
    public void SetMatrix4x4(string name, Matrix4x4 value)
    {
        Floats.Remove(name);
        Vectors.Remove(name);
        Matrices[name] = [
            value.M11,
            value.M12,
            value.M13,
            value.M14,
            value.M21,
            value.M22,
            value.M23,
            value.M24,
            value.M31,
            value.M32,
            value.M33,
            value.M34,
            value.M41,
            value.M42,
            value.M43,
            value.M44,
        ];
    }

    private int? _hash;

    public override int GetHashCode()
    {
        _hash ??= HashCode.Combine(Name);   // 仅 init-only Name；可变字典不入哈希（Equals 仍内容比较）
        return _hash.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not MaterialLegacy m)
            return false;
        if (
            Name != m.Name
            || Floats.Count != m.Floats.Count
            || Vectors.Count != m.Vectors.Count
            || Matrices.Count != m.Matrices.Count
        )
            return false;
        foreach (var kv in Floats)
            if (!m.Floats.TryGetValue(kv.Key, out var v) || kv.Value != v)
                return false;
        foreach (var kv in Vectors)
            if (!m.Vectors.TryGetValue(kv.Key, out var v) || kv.Value != v)
                return false;
        foreach (var kv in Matrices)
        {
            if (!m.Matrices.TryGetValue(kv.Key, out var other) || kv.Value.Length != other.Length)
                return false;
            for (int i = 0; i < kv.Value.Length; i++)
                if (kv.Value[i] != other[i])
                    return false;
        }
        return true;
    }

    /// <summary>释放回调：引用计数归零时由 AssetManager 触发（主纹理级联挂接点）</summary>
    internal event Action<AssetManager>? MaterialDisposed;

    /// <summary>引用归零通知（AssetManager.TryRelease 经 IReleaseAwareAsset 调用；携带释放方管理器实例供级联）</summary>
    void IReleaseAwareAsset.OnAssetReleased(AssetManager manager) =>
        MaterialDisposed?.Invoke(manager);
}
