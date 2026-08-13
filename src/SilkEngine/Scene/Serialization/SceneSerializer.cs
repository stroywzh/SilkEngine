using System.Text.Json;
using System.Text.Json.Nodes;
using SilkEngine;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 场景序列化器：Serialize(Scene)→.scene JSON 字符串；Deserialize(string)→Scene。
/// 反序列化自动组装：new GameObject → AttachSerializedData → 逐组件
/// ComponentTypeRegistry.Resolve + AddComponent（工厂内 ReadFrom）→ ClearSerializedData
/// → 递归子对象 SetParent → AddRootObject。
/// </summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>场景 → .scene JSON 字符串（根对象列表，子对象经 Children 递归嵌套）。</summary>
    public static string Serialize(SilkEngine.Scene.Scene scene)
    {
        var root = new JsonObject { ["Name"] = scene.Name, ["GameObjects"] = new JsonArray() };
        var arr = root["GameObjects"]!.AsArray();
        var warnedOnce = new HashSet<string>();
        foreach (var go in scene.GetRootGameObjects())
            arr.Add(WriteGameObject(go, warnedOnce));
        return root.ToJsonString(Options);
    }

    /// <summary>.scene JSON 字符串 → 新场景（组件经工厂创建并读取序列化数据）。</summary>
    public static SilkEngine.Scene.Scene Deserialize(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Scene JSON root is not an object");

        var scene = new SilkEngine.Scene.Scene(root["Name"]?.GetValue<string>() ?? "Untitled");
        if (root["GameObjects"] is JsonArray arr)
        {
            foreach (var n in arr)
            {
                if (n is JsonObject goNode)
                    scene.AddRootObject(ReadGameObject(goNode));
            }
        }
        return scene;
    }

    private static JsonObject WriteGameObject(GameObject go, HashSet<string> warnedOnce)
    {
        var node = new JsonObject { ["Name"] = go.Name, ["Components"] = new JsonObject() };
        var comps = node["Components"]!.AsObject();
        var t = go.Transform;
        comps["Transform"] = new JsonObject
        {
            ["LocalPosition"] = new JsonArray(t.LocalPosition.X, t.LocalPosition.Y, t.LocalPosition.Z),
            ["LocalRotation"] = new JsonArray(t.LocalRotation.X, t.LocalRotation.Y, t.LocalRotation.Z, t.LocalRotation.W),
            ["LocalScale"] = new JsonArray(t.LocalScale.X, t.LocalScale.Y, t.LocalScale.Z),
        };
        foreach (var c in go._components)
        {
            var compNode = new JsonObject();
            c.WriteTo(new SerializedNode(compNode));
            comps[c.GetType().FullName!] = compNode;
            if (compNode.Count == 0 && warnedOnce.Add(c.GetType().FullName!))
                Log.Warn($"[SceneSerializer] component '{c.GetType().FullName}' 序列化内容为空（无字段/字段全排除/未标记 SerializableInternal）");
        }
        if (t.Children.Count > 0)
        {
            var children = new JsonArray();
            foreach (var ch in t.Children)
                children.Add(WriteGameObject(ch.GameObject!, warnedOnce));
            node["Children"] = children;
        }
        return node;
    }

    private static GameObject ReadGameObject(JsonObject node)
    {
        var go = new GameObject(node["Name"]?.GetValue<string>() ?? "GameObject");
        go.AttachSerializedData(node);

        if (node["Components"] is JsonObject comps)
        {
            foreach (var kv in comps)
            {
                if (kv.Key == "Transform" || kv.Value is not JsonObject compNode)
                    continue;   // Transform 已由 AttachSerializedData 恢复
                var factory = ComponentTypeRegistry.Resolve(kv.Key);
                if (factory == null)
                    continue;   // 未知类型：警告已记，跳过
                go.AddComponent(factory());
            }
        }

        go.ClearSerializedData();

        if (node["Children"] is JsonArray children)
        {
            foreach (var n in children)
            {
                if (n is JsonObject childNode)
                {
                    var child = ReadGameObject(childNode);
                    child.Transform.SetParent(go.Transform);
                }
            }
        }
        return go;
    }
}
