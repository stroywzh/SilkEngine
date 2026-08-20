namespace SilkEngine.Tests.Input
{
    using SilkEngine.Core;
    using SilkEngine.InputSystem;
    using SilkEngine.Math;

    public class InputTests
    {
        private class FakeProvider : IInputProvider
        {
            public bool A, D, W, S;
            public Vector2 Pos;
            public float Scroll;
            public void Initialize(Silk.NET.Windowing.IWindow w) { }
            public void Dispose() { }
            public void UpdateInput(KeyboardState kb, MouseState ms)
            {
                kb.SetKey(KeyCode.A, A);
                kb.SetKey(KeyCode.D, D);
                kb.SetKey(KeyCode.W, W);
                kb.SetKey(KeyCode.S, S);
                ms.SetPosition(Pos);
                ms.ScrollDelta = Scroll;
            }
        }

        private class TrackingProvider : IInputProvider
        {
            public bool Disposed;
            public void Initialize(Silk.NET.Windowing.IWindow w) { }
            public void Dispose() => Disposed = true;
            public void UpdateInput(KeyboardState kb, MouseState ms) { }
        }

        [Fact]
        public void GetKeyDown_DelegatesToKeyboard()
        {
            var p = new FakeProvider { D = true };
            Input.SetProvider(p);
            Input.Update();
            Assert.True(Input.GetKeyDown(KeyCode.D));
        }

        [Fact]
        public void MousePosition_DelegatesToMouse()
        {
            var p = new FakeProvider { Pos = new Vector2(42, 99) };
            Input.SetProvider(p);
            Input.Update();
            Assert.Equal(new Vector2(42, 99), Input.MousePosition);
        }

        [Fact]
        public void GetAxis_Horizontal_Positive()
        {
            var p = new FakeProvider { D = true };
            Input.SetProvider(p);
            Time.DeltaTime = 0.016f;
            Input.Update();
            Input.Update();
            float v = Input.GetAxis("Horizontal");
            Assert.True(v > 0f);
        }

    [Fact]
    public void GetAxis_Horizontal_Released_ReturnsToZero()
    {
        var p = new FakeProvider { D = true };
        Input.SetProvider(p);
        for (int i = 0; i < 30; i++) Input.Update();
        p.D = false;
        for (int i = 0; i < 30; i++) Input.Update();
        Assert.True(Input.GetAxis("Horizontal") < 0.1f);
    }

    [Fact]
    public void SetProvider_DisposesOldProvider()
    {
        var old = new TrackingProvider();
        var next = new TrackingProvider();
        Input.SetProvider(old);
        Input.SetProvider(next);
        Assert.True(old.Disposed);
        Assert.False(next.Disposed);
        Input.SetProvider(new FakeProvider()); // 清理静态 provider，避免影响同类的后续测试
    }
}
}
