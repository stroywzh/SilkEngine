using SilkEngine;
using SilkEngine.InputSystem;

namespace SilkEngine.Tests.Input;

public class KeyboardStateTests
{
    [Fact]
    public void GetKey_AfterSetKey_ReturnsTrue()
    {
        var kb = new KeyboardState();
        kb.SetKey(KeyCode.A, true);
        Assert.True(kb.GetKey(KeyCode.A));
    }

    [Fact]
    public void GetKeyDown_FirstFrame_ReturnsTrue()
    {
        var kb = new KeyboardState();
        kb.SwapBuffers();                       // 帧边界：prev为空
        kb.SetKey(KeyCode.Space, true);         // 本帧首次按下
        Assert.True(kb.GetKeyDown(KeyCode.Space));
    }

    [Fact]
    public void GetKeyDown_HeldSecondFrame_ReturnsFalse()
    {
        var kb = new KeyboardState();
        kb.SetKey(KeyCode.W, true);
        kb.SwapBuffers();
        kb.SetKey(KeyCode.W, true);
        kb.SwapBuffers();
        kb.SetKey(KeyCode.W, true);
        Assert.False(kb.GetKeyDown(KeyCode.W));
    }

    [Fact]
    public void GetKeyUp_OnRelease_ReturnsTrue()
    {
        var kb = new KeyboardState();
        kb.SetKey(KeyCode.D, true);
        kb.SwapBuffers();
        kb.SetKey(KeyCode.D, false);
        Assert.True(kb.GetKeyUp(KeyCode.D));
    }

    [Fact]
    public void AnyKey_WhenPressed_ReturnsTrue()
    {
        var kb = new KeyboardState();
        kb.SetKey(KeyCode.A, true);
        Assert.True(kb.AnyKey);
    }
}
