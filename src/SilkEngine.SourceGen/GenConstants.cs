namespace SilkEngine.SourceGen;

/// <summary>
/// 生成器引用的核心类型元数据名常量。
/// 跨部分契约 C4/C7：Part 3 命名空间迁移时同步更新移动类型常量；
/// 任务 5 下沉时把 SerializedNode/ComponentTypeRegistry 切到 SilkEngine.Scene.Serialization。
/// </summary>
internal static class GenConstants
{
    /// <summary>Component 基类（Part 3 迁移后位于 SilkEngine.Scene）。</summary>
    public const string Component = "SilkEngine.Scene.Component";

    /// <summary>序列化节点（任务 5 起固定为 SilkEngine.Scene.Serialization）。</summary>
    public const string SerializedNode = "SilkEngine.Scene.Serialization.SerializedNode";

    /// <summary>字段排除特性（任务 2 创建，最终位置）。</summary>
    public const string NoSerializeFieldAttribute = "SilkEngine.Scene.Serialization.NoSerializeFieldAttribute";

    /// <summary>引擎内部序列化组件标记（任务 2 创建，最终位置）。</summary>
    public const string SerializableInternalAttribute = "SilkEngine.Scene.Serialization.SerializableInternalAttribute";

    /// <summary>组件类型注册表（任务 5 起固定为 SilkEngine.Scene.Serialization）。</summary>
    public const string ComponentTypeRegistry = "SilkEngine.Scene.Serialization.ComponentTypeRegistry";

    /// <summary>白名单标量（原生类型化 get/set）。</summary>
    public const string Int32 = "global::System.Int32";
    public const string Single = "global::System.Single";
    public const string Boolean = "global::System.Boolean";
    public const string String = "global::System.String";
    public const string Guid = "global::System.Guid";
    public const string Vector3 = "global::SilkEngine.Math.Vector3";
    public const string Quaternion = "global::SilkEngine.Math.Quaternion";

    /// <summary>白名单资产引用（→ GUID；AssetRefCodec 任务 5 创建后接通编译）。</summary>
    public const string Shader = "global::SilkEngine.Render.Shader";
    public const string Mesh = "global::SilkEngine.Render.Mesh";
    public const string Material = "global::SilkEngine.Render.Material";
    public const string Texture2D = "global::SilkEngine.Core.Assets.Texture2D";

    /// <summary>资产编解码桥（任务 5 创建，最终位置；任务 4 仅生成文本、不参与编译）。</summary>
    public const string AssetRefCodec = "SilkEngine.Scene.Serialization.AssetRefCodec";
}
