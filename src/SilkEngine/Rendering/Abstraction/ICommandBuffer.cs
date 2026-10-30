namespace SilkEngine.Rendering.Abstraction;

/// <summary>A writable lease over backend-owned command storage.</summary>
public interface ICommandBuffer
{
    /// <summary>Gets the command count while this buffer is writable.</summary>
    int CommandsCount { get; }

    /// <summary>Copies one command payload into this buffer.</summary>
    void Record(int commandId, ReadOnlySpan<byte> payload);
}
