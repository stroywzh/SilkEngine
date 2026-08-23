using System.Text.Json;
using System.Text.Json.Nodes;
using SilkEngine.Core;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 场景序列化器：Serialize(Scene)→.scene JSON 字符串；Deserialize(string)→Scene。
/// 反序列化自动组装：DeserializationContext 挂载节点 → Transform 恢复 → 逐组件
/// 组件键解析（GUID 优先，FullName 回退兼容旧格式文件）+ AddComponent（工厂内经 context 读取 ReadFrom 数据）
/// → context.Detach → 递归子对象 SetParent → AddRootObject。
/// </summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>场景 → .scene JSON 字符串（根对象列表，子对象经 Children 递归嵌套）。</summary>
    /// <param name="scene">要序列化的场景</param>
    /// <returns>.scene JSON 字符串（缩进格式）</returns>
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
    /// <param name="json">.scene JSON 字符串</param>
    /// <returns>反序列化得到的新场景</returns>
    /// <exception cref="System.Text.Json.JsonException">JSON 根节点不是对象（格式非法）</exception>
    public static SilkEngine.Scene.Scene Deserialize(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new JsonException("Scene JSON root is not an object");

        var scene = new SilkEngine.Scene.Scene(root["Name"]?.GetValue<string>() ?? "Untitled");
        if (root["GameObjects"] is JsonArray arr)
        {
            var ctx = new DeserializationContext();   // 局部持有：反序列化窗口内显式传递，不注册全局 Services（避免并行测试污染）
            foreach (var n in arr)
            {
                if (n is JsonObject goNode)
                    scene.AddRootObject(ReadGameObject(goNode, ctx));
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
            var key = ComponentTypeRegistry.GetGuid(c.GetType()).ToString();
            comps[key] = compNode;
            if (compNode.Count == 0 && warnedOnce.Add(key))
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

    private static GameObject ReadGameObject(JsonObject node, DeserializationContext ctx)
    {
        var go = new GameObject(node["Name"]?.GetValue<string>() ?? "GameObject");
        ctx.Attach(go, node);
        RestoreTransform(go, node);

        if (node["Components"] is JsonObject comps)
        {
            NormalizeComponentKeys(comps);   // GUID 键 → FullName 键：工厂解析与组件数据查找共用同一键空间
            foreach (var kv in comps)
            {
                if (kv.Key == "Transform" || kv.Value is not JsonObject compNode)
                    continue;   // Transform 已由 RestoreTransform 恢复
                var factory = ComponentTypeRegistry.Resolve(kv.Key);
                if (factory == null)
                    continue;   // 未知类型：警告已记，跳过
                go.AddComponent(factory(), null);   // A3 后组件按默认值挂载，不再恢复序列化状态
            }
        }

        ctx.Detach(go);   // 组件均完成 ReadFrom，释放中间态

        if (node["Children"] is JsonArray children)
        {
            foreach (var n in children)
            {
                if (n is JsonObject childNode)
                {
                    var child = ReadGameObject(childNode, ctx);
                    child.Transform.SetParent(go.Transform);
                }
            }
        }
        return go;
    }

    /// <summary>
    /// 组件键归一化：GUID 键（新格式）原地改写为类型全名（ResolveGuid 派生；GUID 优先、FullName 键原样保留兼容旧文件）。
    /// 组件数据节点查找（GameObject.InitializeComponent 按 FullName 取数）与工厂解析共用此键空间。
    /// </summary>
    /// <param name="comps">"Components" 节点（原地改写）</param>
    private static void NormalizeComponentKeys(JsonObject comps)
    {
        foreach (var kv in comps.ToList())
        {
            if (Guid.TryParse(kv.Key, out var g) && ComponentTypeRegistry.ResolveGuid(g) is { } t)
            {
                comps.Remove(kv.Key);
                comps[t.FullName!] = kv.Value;
            }
        }
    }

    /// <summary>恢复 Transform 序列化值（仅覆盖存在键的字段，缺键保留默认）。</summary>
    /// <param name="go">目标对象</param>
    /// <param name="node">对象的序列化节点</param>
    private static void RestoreTransform(GameObject go, JsonObject node)
    {
        if (node["Components"] is JsonObject comps && comps["Transform"] is JsonObject t)
        {
            var sn = new SerializedNode(t);
            if (t.ContainsKey("LocalPosition"))
                go.Transform.LocalPosition = sn.GetVector3("LocalPosition");
            if (t.ContainsKey("LocalRotation"))
                go.Transform.LocalRotation = sn.GetQuaternion("LocalRotation");
            if (t.ContainsKey("LocalScale"))
                go.Transform.LocalScale = sn.GetVector3("LocalScale");
        }
    }
}
