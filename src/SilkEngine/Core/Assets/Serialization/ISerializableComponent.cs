using SilkEngine.Scene.Serialization;

namespace SilkEngine.Core.Assets.Serialization;

/// <summary>
/// 组件序列化契约：WriteTo 将字段写入节点，ReadFrom 从节点恢复字段。
/// 由组件工厂在 OnAwake 之后、RecomputeActiveState 之前自动调用 ReadFrom。
/// </summary>
public interface ISerializableComponent
{
    void ReadFrom(SerializedNode node);
    void WriteTo(SerializedNode node);
}
