namespace SilkEngine.SourceGen;

/// <summary>
/// 生成器引用的核心类型元数据名常量。
/// 跨部分契约 C4/C7：Part 3 命名空间迁移时同步更新移动类型常量；
/// 任务 5 下沉时把 SerializedNode/ComponentTypeRegistry 切到 SilkEngine.Scene.Serialization。
/// </summary>
internal static class GenConstants
{
    /// <summary>Component 基类（Part 3 迁至 SilkEngine.Scene 后更新为 "SilkEngine.Scene.Component"）。</summary>
    public const string Component = "SilkEngine.Component";

    /// <summary>序列化节点（任务 5 下沉后更新为 "SilkEngine.Scene.Serialization.SerializedNode"）。</summary>
    public const string SerializedNode = "SilkEngine.Core.Assets.Serialization.SerializedNode";

    /// <summary>字段排除特性（任务 2 创建，最终位置）。</summary>
    public const string NoSerializeFieldAttribute = "SilkEngine.Scene.Serialization.NoSerializeFieldAttribute";

    /// <summary>引擎内部序列化组件标记（任务 2 创建，最终位置）。</summary>
    public const string SerializableInternalAttribute = "SilkEngine.Scene.Serialization.SerializableInternalAttribute";

    /// <summary>组件类型注册表（任务 5 下沉后更新为 "SilkEngine.Scene.Serialization.ComponentTypeRegistry"）。</summary>
    public const string ComponentTypeRegistry = "SilkEngine.Core.Assets.Serialization.ComponentTypeRegistry";
}
