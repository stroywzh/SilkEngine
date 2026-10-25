// using Silk.NET.Input;
using SilkEngine;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Runtime;

Application.RenderBackend = new SilkOpenGLBackend();
Application.RenderPipeline = new BuiltInPipeline();
var main = ThreadFactory.StartMainThread();

// IInputContext _input;
// bool shouldClose = false;
try
{
    // 我操你大爷的，这个好像也得从头写一个胶水层
    // SpinWait.SpinUntil(() => main.State == EngineThreadState.Running);
    // if (Application.RenderBackend is SilkOpenGLBackend skb)
    // {
    //     // Running is entered before Initialize, so the window may still be null here.
    //     SpinWait.SpinUntil(() => skb.Inited);
    //     // TODO: GLFW input must be created on the window/render thread; this entry-thread path
    //     // currently throws GlfwException (gamepad) and makes DoEvents NRE.
    //     _input = skb.ForRegisterInput()!.CreateInput();

    //     for (int i = 0; i < _input.Keyboards.Count; i++)
    //     {
    //         _input.Keyboards[i].KeyDown += OnKeyDown;
    //         // _input.Keyboards[i].KeyUp += OnKeyUp;
    //     }
    // }

    // Entry thread waits without driving GL. The render owner pumps the prototype window.

    // 反正现在先在这里阻塞吧，以后这东西就是个startup，跑完就结束的。
    SpinWait.SpinUntil(() => !main.IsAlive);
}
finally
{
    ThreadRegistry.StopMainThread();
    Log.Info("All stopped.");
}

// void OnKeyDown(IKeyboard keyboard, Key key, int keyCode)
// {
//     if (key == Key.Escape)
//     {
//         Log.Info("Game Shoule Exit...");
//         shouldClose = true;
//     }
// }

// void OnKeyUp(IKeyboard keyboard, Key key, int keyCode) { }

