using SilkEngine.Host;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// 业务创建 API（阶段 3 任务 1）：经 EngineHost + SceneManager.Create 的统一创建入口，
/// SceneContext 显式携带 Registry/AssetService/Scene，组件经上下文注册（不依赖 Services 回退链）。
/// </summary>
[Collection("Assets")]
public sealed class SceneCreationApiTests : IDisposable
{
    private readonly EngineHost _host;

    public SceneCreationApiTests()
    {
        _host = EngineHost.Create(b => b.UseHeadlessForTests());
        _host.Initialize();
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void CreateGameObject_BindsSceneRegistersComponentAndRunsAwake()
    {
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);

        var go = scene.CreateGameObject("Player");
        var component = go.AddComponent<ProbeComponent>();

        Assert.Same(scene, go.SceneForTests);
        Assert.True(component.Awoke);
        Assert.True(scene.Contains(go));

        _host.Loop.StepFrame();

        Assert.NotEmpty(_host.SceneManager.Registry.GetOfType<ProbeComponent>());
    }

    [Fact]
    public void AddRootObject_OutsideSceneFactory_IsNotRequiredForBusinessCode()
    {
        var scene = _host.SceneManager.Create("Main");

        var go = scene.CreateGameObject("Child");

        Assert.True(scene.Contains(go));
    }

    [Fact]
    public void CreateGameObject_UsesSceneManagerInjectedRegistry()
    {
        var scene = _host.SceneManager.Create("Main");
        _host.SceneManager.LoadScene(scene);

        scene.CreateGameObject("Cube").AddComponent<ProbeComponent>();
        _host.Loop.StepFrame();

        Assert.Same(_host.SceneManager.Registry, scene.Context.Registry);
        Assert.NotEmpty(scene.Context.Registry.GetOfType<ProbeComponent>());
    }

    private sealed class ProbeComponent : MonoBehaviour
    {
        public bool Awoke;

        public override void OnAwake() => Awoke = true;
    }
}
