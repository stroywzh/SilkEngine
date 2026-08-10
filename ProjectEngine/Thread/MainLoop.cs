using System;

namespace ProjectEngine;

public class MainLoop : IDisposable
{
    private float _accumulator;
    private float _fixedDt = 0.02f;

    public float FixedDeltaTime
    {
        get => _fixedDt;
        set { _fixedDt = value; Time.FixedDeltaTime = value; }
    }

    public MainLoop() => Time.FixedDeltaTime = _fixedDt;

    public void Tick(float deltaTime)
    {
        _accumulator += deltaTime;
        while (_accumulator >= _fixedDt)
        {
            SceneManager.FixedTick(_fixedDt);
            _accumulator -= _fixedDt;
        }
        SceneManager.Tick(deltaTime);
        SceneManager.LateTick();
        SceneManager.ProcessDestroys(deltaTime);
    }

    public void LateTick(float deltaTime) => SceneManager.PostRender();

    public void Stop() { }
    public void Dispose() { }
}
