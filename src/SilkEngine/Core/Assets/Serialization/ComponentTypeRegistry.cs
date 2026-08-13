using System;
using System.Collections.Generic;

namespace SilkEngine.Core.Assets.Serialization;

/// <summary>
/// 零反射组件类型注册表：类型全名 → 工厂。引擎组件自注册，用户组件经 Register 扩展。
/// </summary>
public static class ComponentTypeRegistry
{
    private static readonly Dictionary<string, Func<Component>> _factories = new()
    {
        ["SilkEngine.MeshRenderer"] = () => new MeshRenderer(),
        ["SilkEngine.Camera"] = () => new Camera(),
    };

    /// <summary>按类型全名解析组件工厂；未命中返回 null 并记录警告。</summary>
    public static Func<Component>? Resolve(string typeFullName)
    {
        if (_factories.TryGetValue(typeFullName, out var factory))
            return factory;
        Log.Warn($"ComponentTypeRegistry: unknown component type '{typeFullName}'");
        return null;
    }

    /// <summary>注册自定义组件工厂（用户组件扩展点）。</summary>
    public static void Register(string typeFullName, Func<Component> factory)
        => _factories[typeFullName] = factory;
}
