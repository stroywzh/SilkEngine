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

    public void Tick(float deltaTime, FrameSnapshot snapshot)
    {
        _accumulator += deltaTime;
        while (_accumulator >= _fixedDt)
        {
            SceneManager.Instance.FixedTick(snapshot, _fixedDt);
            _accumulator -= _fixedDt;
        }
        SceneManager.Instance.Tick(snapshot, deltaTime);
        SceneManager.Instance.LateTick(snapshot);
        // 注意：销毁处理由 FrameSnapshotManager.CommitPending 在帧末统一执行，
        // 此处不调用 ProcessDestroys（避免双重处理）
    }

    public void LateTick(float deltaTime, FrameSnapshot snapshot)
        => SceneManager.Instance.PostRender(snapshot);

    public void Stop() { }

    public void Dispose() { }
}
