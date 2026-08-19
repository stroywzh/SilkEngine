using System;
using System.Collections.Generic;

namespace SilkEngine.Core;

/// <summary>
/// 引擎内部服务定位器：Initialize 期注册管理者实例，运行期按类型取用（fail-fast）。
/// 仅引擎内部可见；跨程序集调用方经 EngineLoop 公开属性取用。
/// </summary>
internal static class Services
{
    private static readonly object _lock = new();
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly List<(Type Type, IDisposable Disposable)> _disposables = new();

    /// <summary>注册服务（初始化期调用；重复注册同一类型抛异常；name 用于日志显示，默认类型全名）</summary>
    public static void Register<T>(T service, string? name = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (_lock)
        {
            if (!_services.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"服务重复注册: {typeof(T).FullName}");
            if (service is IDisposable d)
                _disposables.Add((typeof(T), d));
            if (LogConfig.Services)
                Log.Info($"[Services] Registered {name ?? typeof(T).FullName}");
        }
    }

    /// <summary>取服务；未注册抛 InvalidOperationException（fail-fast）</summary>
    public static T Get<T>()
        where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
        }
        throw new InvalidOperationException($"服务未注册: {typeof(T).FullName}");
    }

    /// <summary>取服务（null 容忍）：未注册返回 false 且 service 为 null。引擎初始化前调用点使用</summary>
    public static bool TryGet<T>(out T? service)
        where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }
        }
        service = null;
        return false;
    }

    /// <summary>注销服务（测试夹具生命周期用；不调用 Dispose，释放由调用方负责）</summary>
    public static void Unregister<T>()
        where T : class
    {
        lock (_lock)
        {
            _services.Remove(typeof(T));
            _disposables.RemoveAll(e => e.Type == typeof(T));
            if (LogConfig.Services)
                Log.Info($"[Services] Unregistered {typeof(T).FullName}");
        }
    }

    /// <summary>反序 Dispose 全部已注册 IDisposable 服务并清空注册表；幂等。关闭后 Get 抛未注册异常</summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            int count = _disposables.Count;
            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Disposable.Dispose();
            _disposables.Clear();
            _services.Clear();
            if (LogConfig.Services)
                Log.Info($"[Services] Shutdown (disposed {count})");
        }
    }
}
