namespace ProjectEngine.Input;

public static class DefaultAxisMapping
{
    public record AxisConfig(string Name, KeyCode Positive, KeyCode Negative,
        float Sensitivity = 1f, float Gravity = 3f);

    public static readonly AxisConfig[] DefaultAxes =
    [
        new("Horizontal", KeyCode.D, KeyCode.A),
        new("Vertical",   KeyCode.W, KeyCode.S),
    ];
}
