using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Serialization;
using SilkEngine.Render;

namespace SilkEngine;

public class MeshRenderer : Component, ISerializableComponent
{
    private Shader? _shader;
    private Mesh? _mesh;
    private Material? _material;

    public Shader? Shader
    {
        get => _shader;
        set => AssetManager.SetTracked(ref _shader, value);
    }

    public Mesh? Mesh
    {
        get => _mesh;
        set => AssetManager.SetTracked(ref _mesh, value);
    }

    public Material? Material
    {
        get => _material;
        set => AssetManager.SetTracked(ref _material, value);
    }

    /// <summary>组件销毁：归还全部资产引用（引用归零的托管资产由帧末卸载）</summary>
    public override void OnDestroy()
    {
        AssetManager.SetTracked(ref _shader, null);
        AssetManager.SetTracked(ref _mesh, null);
        AssetManager.SetTracked(ref _material, null);
    }

    /// <summary>反序列化：GUID 字符串 → 经属性赋值（SetTracked 计数闭环）</summary>
    public void ReadFrom(SerializedNode node)
    {
        Shader = Resolve<Shader>(node.GetString("Shader"));
        Mesh = Resolve<Mesh>(node.GetString("Mesh"));
        Material = Resolve<Material>(node.GetString("Material"));
    }

    /// <summary>序列化：仅写出托管资产（缓存有条目）的 GUID；null/非托管跳过</summary>
    public void WriteTo(SerializedNode node)
    {
        WriteGuid(node, "Shader", Shader);
        WriteGuid(node, "Mesh", Mesh);
        WriteGuid(node, "Material", Material);
    }

    private static T? Resolve<T>(string? guid)
        where T : class, IAsset
    {
        if (guid is null || !Guid.TryParse(guid, out var g))
            return null;
        var entry = AssetManager.Cache.Find(g);
        return entry is { Data: T asset } ? asset : null;
    }

    private static void WriteGuid(SerializedNode node, string key, IAsset? asset)
    {
        if (asset != null && AssetManager.TryGetGuid(asset, out var guid) && guid != Guid.Empty)
            node.SetString(key, guid.ToString());
    }
}
