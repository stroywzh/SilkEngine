//TODO:建立和维护一个虚拟文件系统
// 设想： 基于用户项目根目录(Root)->任意文件，索引存储到VirturalFileSystem的DB，然后资产加载卸载都基于这个VirturalFileSystem的内容。
// 每次发生文件更改就刷新目录存储的DB
// AssetManager需要一个基础的虚拟文件系统才行

using System.Collections.Concurrent;

namespace SilkEngine.Assets.VirtualFileSystem;

public class VirtualFileSystem
{
    ConcurrentDictionary<Guid, VirtualNode> nodes = new();
    ConcurrentDictionary<Guid?, List<Guid>> _childrens = new();

    public VirtualFileSystem() { }
}
