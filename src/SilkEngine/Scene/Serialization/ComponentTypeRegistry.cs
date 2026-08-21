using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SilkEngine.Core;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 零反射组件类型注册表：类型全名 → 工厂。引擎组件自注册（静态构造），用户组件经 Register&lt;T&gt; 或 Register 扩展。
/// 注册键用 typeof(T).FullName 动态取（Part 3 命名空间迁移自动适应，契约 C4）。
/// 序列化组件键为确定性 GUID（GetGuid：MD5(FullName) 派生）——类型重命名/移命名空间即改变 GUID（重命名即断），
/// 旧格式 FullName 键文件仍经读取回退兼容；Register(string, Func) 无类型信息，GUID 索引在首次序列化该类型时惰性建立。
/// </summary>
public static class ComponentTypeRegistry
{
    private static readonly Dictionary<string, Func<Component>> _factories = new();
    private static readonly Dictionary<Type, Guid> _guids = new();
    private static readonly Dictionary<Guid, Type> _byGuid = new();

    static ComponentTypeRegistry()
    {
        Register<MeshRenderer>();
        Register<Camera>();
    }

    /// <summary>按类型全名解析组件工厂；未命中返回 null 并记录警告。</summary>
    /// <param name="typeFullName">组件类型全名（typeof(T).FullName）</param>
    /// <returns>组件工厂委托；未注册类型返回 null</returns>
    public static Func<Component>? Resolve(string typeFullName)
    {
        if (_factories.TryGetValue(typeFullName, out var factory))
            return factory;
        Log.Warn($"ComponentTypeRegistry: unknown component type '{typeFullName}'");
        return null;
    }

    /// <summary>类型确定性 GUID（MD5(FullName) 派生；重命名即断——文档注明）。</summary>
    /// <param name="type">组件类型</param>
    /// <returns>确定性 GUID（同类型恒定）</returns>
    public static Guid GetGuid(Type type)
    {
        if (_guids.TryGetValue(type, out var g)) return g;
        g = new Guid(MD5.HashData(Encoding.UTF8.GetBytes(type.FullName!)));
        _guids[type] = g;
        _byGuid[g] = type;
        return g;
    }

    /// <summary>GUID 解析类型；未命中 null（调用方回退 FullName）。</summary>
    /// <param name="guid">组件类型 GUID</param>
    /// <returns>类型；未派生/未注册返回 null</returns>
    public static Type? ResolveGuid(Guid guid) =>
        _byGuid.TryGetValue(guid, out var t) ? t : null;

    /// <summary>泛型注册（生成器 SENG003 扫描目标）：typeof(T).FullName → () =&gt; new T()，并同步 GUID 索引。</summary>
    public static void Register<T>() where T : Component, new()
    {
        Register(typeof(T).FullName!, () => new T());
        GetGuid(typeof(T));
    }

    /// <summary>注册自定义组件工厂（用户组件扩展点，向后兼容保留；GUID 索引惰性：首次序列化该类型时建立）。</summary>
    /// <param name="typeFullName">组件类型全名（序列化键）</param>
    /// <param name="factory">创建组件实例的工厂委托</param>
    public static void Register(string typeFullName, Func<Component> factory)
        => _factories[typeFullName] = factory;
}
