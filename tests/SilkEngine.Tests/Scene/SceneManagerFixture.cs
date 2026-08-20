using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 场景夹具：每测试类新建 SceneManager 实例（ctor 自注册 Services 并订阅 Object.DestroyHandler）；
/// 构造后立即注销 Services 注册（本夹具仅用实例、无 ambient 依赖——协调裁决 C1，AddComponent 回退链
/// TryGet 静默不注册），消除整类生命周期内的注册窗口；Dispose 注销（幂等）+ 解绑 DestroyHandler
/// </summary>
public sealed class SceneManagerFixture : IDisposable
{
    public SceneManager Manager { get; }

    public SceneManagerFixture()
    {
        Manager = new();
        Services.Unregister<SceneManager>();
    }

    public void Dispose()
    {
        Services.Unregister<SceneManager>();
        Manager.Dispose();
    }
}
