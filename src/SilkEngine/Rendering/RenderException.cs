namespace SilkEngine.Rendering;

public class RenderException : Exception
{
    public RenderException(string? message)
        : base(message) { }

    public RenderException(string? message, Exception? exception)
        : base(message, exception) { }
}
