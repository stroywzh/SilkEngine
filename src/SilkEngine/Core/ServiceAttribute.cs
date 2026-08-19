using System;

namespace SilkEngine.Core;

/// <summary>
/// 服务自动注册标记：源生成器（ServiceRegistrationGenerator）扫描并按 (Priority 升序, 类名升序) 生成注册代码。
/// Priority 数值越小越先注册 → Services.Shutdown 反序时越晚释放；负值区段为基础设施（如 ThreadManager=-10000）；
/// Name 默认类名。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceAttribute : Attribute
{
    public ServiceAttribute(int priority = 0, string? name = null)
    {
        Priority = priority;
        Name = name;
    }

    public int Priority { get; }
    public string? Name { get; }
}
