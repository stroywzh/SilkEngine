using SilkEngine.InputSystem;
using SilkEngine.Math;

namespace SilkEngine.Tests.InputSystem;

/// <summary>
/// 可配置动作输入（阶段 3 任务 3）：InputActionMap 声明 Button/Axis/MouseDelta 绑定，
/// InputActionService 每帧采样键盘/鼠标解析动作值（GetButton/GetButtonDown/GetButtonUp/GetAxis/GetMouseDelta）。
/// </summary>
public class InputActionTests
{
    private static KeyboardState Pressed(params KeyCode[] keys)
    {
        var kb = new KeyboardState();
        kb.SwapBuffers();
        foreach (var key in keys)
            kb.SetKey(key, true);
        return kb;
    }

    [Fact]
    public void ActionMap_ResolvesButtonAndAxisFromConfiguredBindings()
    {
        var state = Pressed(KeyCode.Space, KeyCode.D);
        var actions = new InputActionService(state);
        actions.AddMap("Gameplay", map =>
        {
            map.Button("Jump", KeyCode.Space);
            map.Axis("MoveX", KeyCode.A, KeyCode.D);
        });

        actions.Update();

        Assert.True(actions.GetButton("Gameplay", "Jump"));
        Assert.Equal(1f, actions.GetAxis("Gameplay", "MoveX"));
    }

    [Fact]
    public void GetButtonDown_AndUp_ReportEdges()
    {
        var kb = new KeyboardState();
        var actions = new InputActionService(kb);
        actions.AddMap("M", map => map.Button("Fire", KeyCode.Space));

        kb.SwapBuffers();
        kb.SetKey(KeyCode.Space, true);
        actions.Update();
        Assert.True(actions.GetButtonDown("M", "Fire"));
        Assert.False(actions.GetButtonUp("M", "Fire"));

        kb.SwapBuffers();
        actions.Update();
        Assert.True(actions.GetButtonUp("M", "Fire"));
        Assert.False(actions.GetButtonDown("M", "Fire"));
    }

    [Fact]
    public void Axis_NegativeAndBothKeys()
    {
        var negative = new InputActionService(Pressed(KeyCode.A));
        negative.AddMap("M", map => map.Axis("X", KeyCode.A, KeyCode.D));
        negative.Update();
        Assert.Equal(-1f, negative.GetAxis("M", "X"));

        var both = new InputActionService(Pressed(KeyCode.A, KeyCode.D));
        both.AddMap("M", map => map.Axis("X", KeyCode.A, KeyCode.D));
        both.Update();
        Assert.Equal(0f, both.GetAxis("M", "X"));
    }

    [Fact]
    public void MouseDelta_ReadsMouseMoveVector()
    {
        var mouse = new MouseState();
        mouse.SwapBuffers();
        mouse.SetPosition(new Vector2(10, 5));
        var actions = new InputActionService(Pressed(), mouse);
        actions.AddMap("M", map => map.MouseDelta("Look"));

        actions.Update();

        Assert.Equal(new Vector2(10, 5), actions.GetMouseDelta("M", "Look"));
    }

    [Fact]
    public void AddMap_DuplicateName_Throws()
    {
        var actions = new InputActionService(Pressed());
        actions.AddMap("M", map => map.Button("A", KeyCode.A));

        Assert.Throws<InvalidOperationException>(() =>
        {
            actions.AddMap("M", map => map.Button("B", KeyCode.B));
        });
    }

    [Fact]
    public void UnconfiguredMapOrAction_ReturnsSafeDefaults()
    {
        var actions = new InputActionService(Pressed(KeyCode.Space));

        Assert.False(actions.GetButton("Missing", "Jump"));
        Assert.Equal(0f, actions.GetAxis("Missing", "MoveX"));
        Assert.Equal(Vector2.Zero, actions.GetMouseDelta("Missing", "Look"));
    }
}
