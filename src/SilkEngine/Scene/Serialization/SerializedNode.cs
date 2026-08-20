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

    /// <summary>包装既有 JsonObject（组件 WriteTo/ReadFrom 的序列化节点）。</summary>
    /// <param name="obj">要包装的 JSON 对象（引用持有，读写即作用于该对象）</param>
    public SerializedNode(JsonObject obj) => _obj = obj;

    /// <summary>序列化为 JSON 字符串（测试/调试用）。</summary>
    public string AsJson() => _obj.ToJsonString();

    /// <summary>读取字符串；键缺失返回 null。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>字符串值；缺失返回 null</returns>
    public string? GetString(string key) =>
        _obj.TryGetPropertyValue(key, out var node) ? node?.GetValue<string>() : null;

    /// <summary>读取整数；键缺失或类型不匹配返回 0。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>整数值；缺失/不匹配返回 0</returns>
    public int GetInt(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<int>(out var i)
            ? i
            : 0;

    /// <summary>读取浮点数；键缺失或类型不匹配返回 0f。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>浮点值；缺失/不匹配返回 0f</returns>
    public float GetFloat(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<float>(out var f)
            ? f
            : 0f;

    /// <summary>读取布尔值；键缺失或类型不匹配返回 false。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>布尔值；缺失/不匹配返回 false</returns>
    public bool GetBool(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonValue v && v.TryGetValue<bool>(out var b)
            ? b
            : false;

    /// <summary>读取 Vector3（JSON 数组 [x, y, z]）；键缺失或长度不足返回 Vector3.Zero。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>Vector3 值；缺失/非法返回 Vector3.Zero</returns>
    public Vector3 GetVector3(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonArray arr && arr.Count >= 3
            ? new Vector3(GetAt(arr, 0), GetAt(arr, 1), GetAt(arr, 2))
            : Vector3.Zero;

    /// <summary>读取 Quaternion（JSON 数组 [x, y, z, w]）；键缺失或长度不足返回 Quaternion.Identity。</summary>
    /// <param name="key">字段键名</param>
    /// <returns>Quaternion 值；缺失/非法返回 Quaternion.Identity</returns>
    public Quaternion GetQuaternion(string key) =>
        _obj.TryGetPropertyValue(key, out var node) && node is JsonArray arr && arr.Count >= 4
            ? new Quaternion(GetAt(arr, 0), GetAt(arr, 1), GetAt(arr, 2), GetAt(arr, 3))
            : Quaternion.Identity;

    /// <summary>写入字符串；null 移除该键。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">字符串值；null 时移除键</param>
    public void SetString(string key, string? value)
    {
        if (value == null)
            _obj.Remove(key);
        else
            _obj[key] = value;
    }

    /// <summary>写入整数。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">整数值</param>
    public void SetInt(string key, int value) => _obj[key] = value;

    /// <summary>写入浮点数。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">浮点值</param>
    public void SetFloat(string key, float value) => _obj[key] = value;

    /// <summary>写入布尔值。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">布尔值</param>
    public void SetBool(string key, bool value) => _obj[key] = value;

    /// <summary>写入 Vector3（JSON 数组 [x, y, z]）。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">Vector3 值</param>
    public void SetVector3(string key, Vector3 value) =>
        _obj[key] = new JsonArray(value.X, value.Y, value.Z);

    /// <summary>写入 Quaternion（JSON 数组 [x, y, z, w]）。</summary>
    /// <param name="key">字段键名</param>
    /// <param name="value">Quaternion 值</param>
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
