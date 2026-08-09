using ProjectEngine.Abstraction;
using ProjectEngine.EngineThreads;
using ProjectEngine.Render;

namespace ProjectEngine;

/// <summary>
/// MainLoop的HeartBeat，为后续WPF嵌入Editor提供支持
/// </summary>
public class EngineLoop : IDisposable
{
    private MainLoop _mainLoop;
    public bool IsRunning => _mainLoop.IsRunning;

    private readonly IRenderBackend _backend;
    private readonly IRenderPipeline _pipeline;

    private bool shouldStop = false;

    /// <summary>
    /// 渲染后端
    /// </summary>
    public IRenderBackend Backend => _backend;

    /// <summary>
    /// 渲染管线
    /// </summary>
    public IRenderPipeline Pipeline => _pipeline;

    public EngineLoop(IRenderBackend backend, IRenderPipeline pipeline)
    {
        _backend = backend;
        _pipeline = pipeline;
        _mainLoop = new MainLoop();
    }

    public void Run()
    {
        while (!shouldStop)
        {
            _mainLoop.Tick(0);
            _backend.WaitForFrame();
            _mainLoop.LateTick();
        }
    }

    /// <summary>
    /// 每帧在 LateUpdate 之后调用
    /// <br/>从活动场景收集所有 MeshRenderer，转换为 DrawCommand 列表并提交给渲染管线
    /// </summary>
    protected virtual void OnRender()
    {
        // var renderers = new List<Mesh>();
        // var drawCommands = new List<DrawCommand>();
        // foreach (var mr in renderers)
        // {
        //     drawCommands.Add(
        //         new SingleDrawCommand
        //         {
        //             Shader = mr.Shader,
        //             Mesh = mr.Mesh,
        //             Material = mr.Material,
        //             Enabled = mr.Enabled,
        //         }
        //     );
        // }
        // _pipeline.Render(drawCommands);
    }

    public void Pause() { }

    public void Stop()
    {
        _mainLoop.Stop();
        Dispose();
    }

    public void Dispose()
    {
        _mainLoop.Dispose();
    }
}
