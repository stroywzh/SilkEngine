using SilkEngine.Core;
using SilkEngine.Scene;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 组件复制语义（阶段 3 任务 4）：Instantiate 遇到未注册组件类型必须显式失败
/// （InvalidOperationException 含组件类型名），禁止静默跳过。
/// </summary>
public class ComponentCloneTests
{
    private sealed class UnregisteredComponent : Component
    {
    }

    private sealed class RegisteredComponent : Component
    {
    }

    [Fact]
    public void Instantiate_UnregisteredComponentType_FailsExplicitly()
    {
        var source = new GameObject("source");
        source.AddComponent<UnregisteredComponent>();

        var ex = Assert.Throws<InvalidOperationException>(() => Object.Instantiate(source));

        Assert.Contains(nameof(UnregisteredComponent), ex.Message);
    }

    [Fact]
    public void Instantiate_RegisteredComponentType_Succeeds()
    {
        ComponentFactory.Register<RegisteredComponent>();
        var source = new GameObject("source");
        source.AddComponent<RegisteredComponent>();

        var clone = (GameObject)Object.Instantiate(source);

        Assert.NotNull(clone.GetComponent<RegisteredComponent>());
    }
}
