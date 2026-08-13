using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 场景夹具：每测试类新建 SceneManager 实例（ctor 订阅 Object.DestroyHandler）；
/// Dispose 解绑（防静态事件累积）。Part 1 的 SceneManager 不注册 Services（ActiveRegistry 仍静态）
/// </summary>
public sealed class SceneManagerFixture : IDisposable
{
    public SceneManager Manager { get; } = new();

    public void Dispose() => Manager.Dispose();
}
