namespace SilkEngine.Assets;

/// <summary>资产载荷抽象：不可变资产数据容器（规范资产载荷，与运行时可变对象隔离）</summary>
public interface IAssetPayload
{
    /// <summary>资产名称（只读）</summary>
    public string Name { get; }
}
