namespace SilkEngine.InputSystem;

/// <summary>
/// 默认虚拟轴映射表：Input.Update 每帧按 sensitivity 逼近 / gravity 归零推进轴值。
/// </summary>
public static class DefaultAxisMapping
{
    /// <summary>虚拟轴配置：正负键映射 + 逼近/归零速率。</summary>
    /// <param name="name">轴名（Input.GetAxis 查询键）</param>
    /// <param name="positive">正向键（按住轴值 +1 逼近）</param>
    /// <param name="negative">负向键（按住轴值 −1 逼近）</param>
    /// <param name="sensitivity">按住时每帧向目标值逼近的速率（默认 1）</param>
    /// <param name="gravity">松开后每帧向 0 回归的速率（默认 3）</param>
    public record AxisConfig(
        string name,
        KeyCode positive,
        KeyCode negative,
        float sensitivity = 1f,
        float gravity = 3f
    );

    /// <summary>内置轴：Horizontal（A/D）、Vertical（W/S）。</summary>
    public static readonly AxisConfig[] DefaultAxes =
    [
        new("Horizontal", KeyCode.D, KeyCode.A),
        new("Vertical", KeyCode.W, KeyCode.S),
    ];
}
