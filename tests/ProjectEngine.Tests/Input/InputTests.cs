namespace ProjectEngine.Tests.Input
{
    using ProjectEngine;
    using ProjectEngine.InputSystem;
    using ProjectEngine.Math;

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
    }
}
