using System;
using System.Collections.Generic;
using SilkEngine.Core;

namespace SilkEngine.Scene.Serialization;

/// <summary>
/// 零反射组件类型注册表：类型全名 → 工厂。引擎组件自注册（静态构造），用户组件经 Register&lt;T&gt; 或 Register 扩展。
/// 注册键用 typeof(T).FullName 动态取（Part 3 命名空间迁移自动适应，契约 C4）。
/// </summary>
public static class ComponentTypeRegistry
{
    private static readonly Dictionary<string, Func<Component>> _factories = new();

    static ComponentTypeRegistry()
    {
        Register<MeshRenderer>();
        Register<Camera>();
    }

    /// <summary>按类型全名解析组件工厂；未命中返回 null 并记录警告。</summary>
    public static Func<Component>? Resolve(string typeFullName)
    {
        if (_factories.TryGetValue(typeFullName, out var factory))
            return factory;
        Log.Warn($"ComponentTypeRegistry: unknown component type '{typeFullName}'");
        return null;
    }

    /// <summary>泛型注册（生成器 SENG003 扫描目标）：typeof(T).FullName → () =&gt; new T()。</summary>
    public static void Register<T>() where T : Component, new()
        => Register(typeof(T).FullName!, () => new T());

    /// <summary>注册自定义组件工厂（用户组件扩展点，向后兼容保留）。</summary>
    public static void Register(string typeFullName, Func<Component> factory)
        => _factories[typeFullName] = factory;
}
