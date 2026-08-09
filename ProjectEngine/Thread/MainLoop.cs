using ProjectEngine.Abstraction;
using ProjectEngine.Render;

namespace ProjectEngine.EngineThreads;

public class MainLoop : IDisposable
{
    private bool _isRunning = false;
    private bool _stopRequested = false;
    public bool IsRunning => _isRunning;

    public void Tick(double deltaTime)
    {
        _isRunning = true;
        _stopRequested = false;
        _isRunning = false;
    }

    public void LateTick() { }

    public void Dispose() { }

    public void Stop()
    {
        Console.WriteLine("MainLoop Stop");
    }
}
