using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>材质源引用：标识材质来源资产（由 AssetId 指向源材质资产，多个实例可共享同一来源）</summary>
public readonly record struct MaterialReference(AssetId AssetId);
