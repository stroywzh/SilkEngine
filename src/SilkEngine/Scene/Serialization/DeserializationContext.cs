using System.Collections.Generic;
using System.Text.Json.Nodes;
using SilkEngine.Scene;

namespace SilkEngine.Scene.Serialization;

/// <summary>反序列化中间态承载：GameObject → 序列化节点（组件挂载后释放）。</summary>
public sealed class DeserializationContext
{
    private readonly Dictionary<GameObject, JsonObject> _pending = new(ReferenceEqualityComparer.Instance);

    /// <summary>挂载对象序列化节点（对象反序列化开始；重复挂载覆盖）。</summary>
    /// <param name="go">反序列化中的目标对象</param>
    /// <param name="data">该对象的序列化节点（"Components" 等键结构）</param>
    public void Attach(GameObject go, JsonObject data) => _pending[go] = data;

    /// <summary>取回对象序列化节点；未挂载返回 false 且 data 为 null。</summary>
    /// <param name="go">目标对象</param>
    /// <param name="data">输出：命中时的序列化节点；未命中为 null</param>
    /// <returns>是否命中</returns>
    public bool TryGet(GameObject go, out JsonObject data) => _pending.TryGetValue(go, out data!);

    /// <summary>释放对象挂载（组件均完成 ReadFrom 后调用）。</summary>
    /// <param name="go">目标对象</param>
    public void Detach(GameObject go) => _pending.Remove(go);

    /// <summary>清空全部挂载（反序列化结束兜底清理）。</summary>
    public void Clear() => _pending.Clear();
}
