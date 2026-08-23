using System;
using System.Collections.Generic;
using SilkEngine.Core;

namespace SilkEngine.Scene;

/// <summary>组件工厂注册表：类型全名 → 无状态工厂（Object.Instantiate 克隆组件按默认值重建）。</summary>
public static class ComponentFactory
{
    private static readonly Dictionary<string, Func<Component>> _factories = new();

    static ComponentFactory()
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
        Log.Warn($"ComponentFactory: unknown component type '{typeFullName}'");
        return null;
    }

    /// <summary>泛型注册：typeof(T).FullName → () =&gt; new T()。</summary>
    public static void Register<T>() where T : Component, new()
        => Register(typeof(T).FullName!, () => new T());

    /// <summary>注册自定义组件工厂（用户扩展点）。</summary>
    /// <param name="typeFullName">组件类型全名</param>
    /// <param name="factory">创建组件实例的工厂委托</param>
    public static void Register(string typeFullName, Func<Component> factory)
        => _factories[typeFullName] = factory;
}
