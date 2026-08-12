namespace SilkEngine.InputSystem;

public static class DefaultAxisMapping
{
    public record AxisConfig(
        string name,
        KeyCode positive,
        KeyCode negative,
        float sensitivity = 1f,
        float gravity = 3f
    );

    public static readonly AxisConfig[] DefaultAxes =
    [
        new("Horizontal", KeyCode.D, KeyCode.A),
        new("Vertical", KeyCode.W, KeyCode.S),
    ];
}
