using SilkEngine;
using SilkEngine.Scene.Serialization;
using SilkEngine.Math;
using SilkEngine.Scene.Serialization;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

/// <summary>生成器集成测试组件：顶层类型（非嵌套），WriteTo/ReadFrom 由生成器自动生成。</summary>
public partial class GenPlayer : MonoBehaviour
{
    public float Speed = 1f;
    public bool Lit = true;
    public string? Label;
    public Guid Id;
    public Vector3 Offset;
    public Quaternion Rotation;
}

public partial class GenStats : MonoBehaviour
{
    public float HP = 100f;
    [NoSerializeField] public float Hidden = 7f;
}

public partial class GenOuter : MonoBehaviour
{
    public float Power = 2f;
    public GenStats Stats = new();
    public string[] Tags = Array.Empty<string>();
}

[Collection("Serialization")]
public class SourceGenIntegrationTests
{
    private static void RegisterAll()
    {
        ComponentTypeRegistry.Register(typeof(GenPlayer).FullName!, () => new GenPlayer());
        ComponentTypeRegistry.Register(typeof(GenOuter).FullName!, () => new GenOuter());
    }

    [Fact]
    public void Roundtrip_WhitelistFields_Preserved()
    {
        RegisterAll();
        var scene = new Scene("Gen");
        var go = new GameObject("P");
        var p = go.AddComponent<GenPlayer>();
        p.Speed = 3.5f;
        p.Lit = false;
        p.Label = "hero";
        p.Id = new Guid("1f2e3d4c-5b6a-7988-99aa-bbccddeeff00");
        p.Offset = new Vector3(1, 2, 3);
        p.Rotation = Quaternion.Euler(0, 90, 0);
        scene.AddRootObject(go);

        var p2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene))
            .GetRootGameObjects()[0].GetComponent<GenPlayer>()!;

        Assert.Equal(3.5f, p2.Speed);
        Assert.False(p2.Lit);
        Assert.Equal("hero", p2.Label);
        Assert.Equal(new Guid("1f2e3d4c-5b6a-7988-99aa-bbccddeeff00"), p2.Id);
        Assert.Equal(new Vector3(1, 2, 3), p2.Offset);
        Assert.Equal(Quaternion.Euler(0, 90, 0), p2.Rotation);
    }

    [Fact]
    public void Roundtrip_NoSerializeField_ExcludedAndFlattened()
    {
        RegisterAll();
        var scene = new Scene("Gen");
        var go = new GameObject("O");
        var o = go.AddComponent<GenOuter>();
        o.Power = 9f;
        o.Stats.HP = 80f;
        o.Stats.Hidden = 99f;
        scene.AddRootObject(go);

        var json = SceneSerializer.Serialize(scene);
        Assert.Contains("Stats_HP", json);
        Assert.DoesNotContain("Hidden", json);

        var o2 = SceneSerializer.Deserialize(json).GetRootGameObjects()[0].GetComponent<GenOuter>()!;
        Assert.Equal(9f, o2.Power);
        Assert.Equal(80f, o2.Stats.HP);
        Assert.Equal(7f, o2.Stats.Hidden);   // 未序列化 → 新实例默认值
    }

    [Fact]
    public void Roundtrip_ExternalTypeField_StjFallback()
    {
        RegisterAll();
        var scene = new Scene("Gen");
        var go = new GameObject("O");
        var o = go.AddComponent<GenOuter>();
        o.Tags = new[] { "a", "b" };
        scene.AddRootObject(go);

        var o2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene))
            .GetRootGameObjects()[0].GetComponent<GenOuter>()!;

        Assert.Equal(new[] { "a", "b" }, o2.Tags);
    }
}
