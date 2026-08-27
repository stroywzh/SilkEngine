namespace SilkEngine.Assets;

/// <summary>资产唯一标识：包装 GUID，独立于虚拟节点 ID 类型，防止资产与节点身份混用</summary>
public readonly record struct AssetId(Guid Value);

/// <summary>虚拟文件系统节点唯一标识：包装 GUID，独立于资产 ID 类型</summary>
public readonly record struct VirtualNodeId(Guid Value);

/// <summary>资产类型标识：稳定字符串键（按类型注册名），供分类与目录查询使用</summary>
public readonly record struct AssetTypeId(string Value);

/// <summary>强类型资产句柄：携带资产 ID，并在编译期保留资产类型</summary>
/// <typeparam name="T">资产类型，必须是引用类型</typeparam>
public readonly record struct AssetHandle<T>(AssetId Id)
    where T : class;

/// <summary>非泛型资产句柄：序列化等需要在运行时解析类型的场景使用</summary>
public readonly record struct UntypedAssetHandle(AssetId Id);
