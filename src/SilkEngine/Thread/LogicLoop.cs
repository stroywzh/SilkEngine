using System;

namespace SilkEngine.Threading;

public class LogicLoop : IDisposable
{
    private float _accumulator;
    private float _fixedDt = 0.02f;

    public float FixedDeltaTime
    {
        get => _fixedDt;
        set
        {
            _fixedDt = value;
            Time.FixedDeltaTime = value;
        }
    }

    public LogicLoop() => Time.FixedDeltaTime = _fixedDt;

    public void Tick(float deltaTime)
    {
        _accumulator += deltaTime;
        while (_accumulator >= _fixedDt)
        {
            SceneManager.Instance.FixedTick(_fixedDt);
            _accumulator -= _fixedDt;
        }
        SceneManager.Instance.Tick(deltaTime);
        SceneManager.Instance.LateTick();
        SceneManager.Instance.ProcessDestroys(deltaTime);
    }

    public void LateTick(float deltaTime) => SceneManager.Instance.PostRender();

    public void TickWithSnapshot(float deltaTime, FrameSnapshot snapshot, ComponentRegistry registry)
    {
        _accumulator += deltaTime;
        while (_accumulator >= _fixedDt)
        {
            SceneManager.Instance.FixedTickWithSnapshot(snapshot, registry, _fixedDt);
            _accumulator -= _fixedDt;
        }
        SceneManager.Instance.TickWithSnapshot(snapshot, registry, deltaTime);
        SceneManager.Instance.LateTickWithSnapshot(snapshot, registry);
        // 注意：销毁处理由 FrameSnapshotManager.CommitPending 在帧末统一执行，
        // 此处不调用 ProcessDestroys（避免双重处理）
    }

    public void LateTickWithSnapshot(float deltaTime, FrameSnapshot snapshot, ComponentRegistry registry)
        => SceneManager.Instance.PostRenderWithSnapshot(snapshot, registry);

    public void Stop() { }

    public void Dispose() { }
}
