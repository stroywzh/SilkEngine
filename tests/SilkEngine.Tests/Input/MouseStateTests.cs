using SilkEngine.InputSystem;
using SilkEngine.Math;

namespace SilkEngine.Tests.Input;

public class MouseStateTests
{
    [Fact]
    public void SetPosition_UpdatesPosition()
    {
        var ms = new MouseState();
        ms.SetPosition(new Vector2(100, 200));
        Assert.Equal(new Vector2(100, 200), ms.Position);
    }

    [Fact]
    public void MoveVector_IsDeltaSinceLastSwap()
    {
        var ms = new MouseState();
        ms.SetPosition(new Vector2(100, 100));
        ms.SwapBuffers();
        ms.SetPosition(new Vector2(105, 103));
        Assert.Equal(new Vector2(5, 3), ms.MoveVector);
    }

    [Fact]
    public void LeftButton_AfterSet_ReturnsTrue()
    {
        var ms = new MouseState();
        ms.SetButton(0, true);
        Assert.True(ms.LeftButton);
    }

    [Fact]
    public void GetButtonDown_FirstFrame_ReturnsTrue()
    {
        var ms = new MouseState();
        ms.SwapBuffers();
        ms.SetButton(1, true);
        Assert.True(ms.GetButtonDown(1));
    }

    [Fact]
    public void ScrollDelta_ResetsAfterSwap()
    {
        var ms = new MouseState();
        ms.ScrollDelta = 1.2f;
        Assert.Equal(1.2f, ms.ScrollDelta);
        ms.SwapBuffers();
        Assert.Equal(0f, ms.ScrollDelta);
    }
}
