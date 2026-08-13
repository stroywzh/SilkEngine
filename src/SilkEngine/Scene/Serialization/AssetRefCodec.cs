using SilkEngine.Core;
using SilkEngine.Core.Assets;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 资产引用编解码桥（MeshRenderer 原 Resolve/WriteGuid 下沉为公共工具，DESIGN 2.5）：
/// SerializedNode ↔ 托管资产 GUID；经 Services 取 AssetManager 实例（契约 C1/C2）。
/// 生成器对资产字段生成的 WriteTo/ReadFrom 唯一外部依赖点。
/// </summary>
public static class AssetRefCodec
{
    /// <summary>写出托管资产 GUID；null/非托管（缓存无条目）/空 GUID/管理器未注册跳过（不写键）。</summary>
    public static void Write(SerializedNode node, string key, IAsset? asset)
    {
        if (asset is null)
            return;
        if (Services.TryGet<AssetManager>(out var am) && am.TryGetGuid(asset, out var guid) && guid != Guid.Empty)
            node.SetString(key, guid.ToString());
    }

    /// <summary>读取 GUID → 缓存资产；缺失/非法/未命中/管理器未注册返回 null（属性赋值路径，setter 自持引用计数）。</summary>
    public static T? Read<T>(SerializedNode node, string key) where T : class, IAsset
    {
        var s = node.GetString(key);
        if (s is null || !Guid.TryParse(s, out var g))
            return null;
        return Services.TryGet<AssetManager>(out var am) ? am.TryResolve<T>(g) : null;
    }

    /// <summary>读取并直接赋值字段（无属性路径）：管理器已注册经 SetTracked 保持引用计数闭环；未注册仅字段赋值。</summary>
    public static void ReadTracked<T>(ref T? field, SerializedNode node, string key) where T : class, IAsset
    {
        var v = Read<T>(node, key);
        if (Services.TryGet<AssetManager>(out var am))
            am.SetTracked(ref field, v);
        else
            field = v;
    }
}
