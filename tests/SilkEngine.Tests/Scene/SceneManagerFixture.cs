using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 场景夹具：每测试类新建 SceneManager 实例（ctor 订阅 Object.DestroyHandler）；
/// Dispose 解绑（防静态事件累积）。不注册 Services——AddComponent 回退链经
/// Services.TryGet 静默不注册（协调裁决 C1），本类测试无 ambient 依赖
/// </summary>
public sealed class SceneManagerFixture : IDisposable
{
    public SceneManager Manager { get; } = new();

    public void Dispose() => Manager.Dispose();
}
