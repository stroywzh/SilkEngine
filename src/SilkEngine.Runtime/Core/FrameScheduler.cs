namespace SilkEngine.Core;

/// <summary>
/// 固定步长逻辑帧调度（原 EngineLoop.TickFrame 职责）：累加器驱动 FixedTick（固定步长值传入），
/// 随后 Tick（本帧 dt）/LateTick；FixedDeltaTime 与 Time.FixedDeltaTime 双向同步。
/// </summary>
public sealed class FrameScheduler
{
    private readonly FixedStepAccumulator _fixedStep = new();

    /// <summary>构造即同步初值到 Time 门面（原 EngineLoop ctor 语义）。</summary>
    public FrameScheduler() => Time.FixedDeltaTime = _fixedStep.FixedDeltaTime;

    /// <summary>固定步长（秒），默认 0.02；非法值抛错由 FixedStepAccumulator 校验。</summary>
    public float FixedDeltaTime
    {
        get => _fixedStep.FixedDeltaTime;
        set
        {
            _fixedStep.FixedDeltaTime = value;
            Time.FixedDeltaTime = value;
        }
    }

    /// <summary>
    /// 推进一帧逻辑更新：Advance 累积 dt 并循环执行固定步长 FixedTick（步长值传入），
    /// 随后 Tick(dt) 与 LateTick —— 顺序与时序保持原 EngineLoop.TickFrame（FixedTick 循环上限由 dt 钳制约束）。
    /// </summary>
    /// <param name="dt">本帧增量时间（秒）</param>
    /// <param name="fixedTick">固定步长回调（参数 = 固定步长）</param>
    /// <param name="tick">逻辑帧回调（参数 = 本帧 dt）</param>
    /// <param name="lateTick">逻辑帧末回调</param>
    public void Tick(float dt, Action<float> fixedTick, Action<float> tick, Action lateTick)
    {
        int steps = _fixedStep.Advance(dt);
        for (int i = 0; i < steps; i++)
            fixedTick(_fixedStep.FixedDeltaTime);
        tick(dt);
        lateTick();
    }
}
