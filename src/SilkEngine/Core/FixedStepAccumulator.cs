namespace SilkEngine.Core;

/// <summary>固定步长累加器：累积增量时间并返回应执行的固定步数，余数保留（零分配、纯状态机）。</summary>
internal sealed class FixedStepAccumulator
{
    /// <summary>固定步长（秒），默认 0.02（与 Time.FixedDeltaTime 初值一致）。</summary>
    public float FixedDeltaTime { get; set; } = 0.02f;

    /// <summary>当前剩余累积（不足一个固定步长的余量，测试断言用）。</summary>
    public float Remainder { get; private set; }

    /// <summary>累加 deltaTime，返回本帧应触发的 FixedTick 次数；余数保留到下一帧。</summary>
    public int Advance(float deltaTime)
    {
        Remainder += deltaTime;
        int count = 0;
        while (Remainder >= FixedDeltaTime)
        {
            Remainder -= FixedDeltaTime;
            count++;
        }
        return count;
    }
}
