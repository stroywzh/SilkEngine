using System;

namespace SilkEngine.Core;

/// <summary>
/// 服务标记
/// <br/>用于管理器自动注册到Service
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public string Name { get; set; }
}
