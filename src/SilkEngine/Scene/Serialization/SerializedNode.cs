using System.Text.Json.Nodes;
using SilkEngine.Math;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 类型化 getter/setter，包装 System.Text.Json 的 JsonObject。
/// 缺失字段返回默认值（不抛异常）：string→null、int→0、float→0f、bool→false、
/// Vector3→Zero、Quaternion→Identity。
/// </summary>
public sealed class SerializedNode
{
    private readonly JsonObject _obj;

    public SerializedNode(JsonObject obj) => _obj = obj;

    /// <summary>序列化为 JSON 字符串（测试/调试用）。</summary>
    public string AsJson() => _obj.ToJsonString();

    public string? GetString(string key) =>
        _obj.TryGetPropertyValue(key, out var node) ? node?.GetValue<string>() : null;

    public int GetInt(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<int>(out var i)
            ? i
            : 0;

    public float GetFloat(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<float>(out var f)
            ? f
            : 0f;

    public bool GetBool(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<bool>(out var b)
            ? b
            : false;

    public Vector3 GetVector3(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonArray arr && arr.Count >= 3
            ? new Vector3(GetAt(arr, 0), GetAt(arr, 1), GetAt(arr, 2))
            : Vector3.Zero;

    public Quaternion GetQuaternion(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonArray arr && arr.Count >= 4
            ? new Quaternion(GetAt(arr, 0), GetAt(arr, 1), GetAt(arr, 2), GetAt(arr, 3))
            : Quaternion.Identity;

    public void SetString(string key, string? value)
    {
        if (value == null)
            _obj.Remove(key);
        else
            _obj[key] = value;
    }

    public void SetInt(string key, int value) => _obj[key] = value;
    public void SetFloat(string key, float value) => _obj[key] = value;
    public void SetBool(string key, bool value) => _obj[key] = value;

    public void SetVector3(string key, Vector3 value) =>
        _obj[key] = new JsonArray(value.X, value.Y, value.Z);

    public void SetQuaternion(string key, Quaternion value) =>
        _obj[key] = new JsonArray(value.X, value.Y, value.Z, value.W);

    /// <summary>键是否存在（区分"缺失"与"默认值"，递归展开与缺失保留语义用）。</summary>
    public bool ContainsKey(string key) => _obj.ContainsKey(key);

    /// <summary>写入 JsonNode 子树（STJ 兜底用）；null 移除键。</summary>
    public void SetRaw(string key, JsonNode? node)
    {
        if (node is null)
            _obj.Remove(key);
        else
            _obj[key] = node;
    }

    /// <summary>读取 JsonNode 子树（STJ 兜底用）；缺失返回 null。</summary>
    public JsonNode? GetRaw(string key) =>
        _obj.TryGetPropertyValue(key, out var node) ? node : null;

    private static float GetAt(JsonArray arr, int index)
    {
        var v = arr[index];
        return v is JsonValue jv && jv.TryGetValue<float>(out var f) ? f : 0f;
    }
}
