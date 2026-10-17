using Xunit;

namespace SilkEngine.Rendering.Tests;

/// <summary>Serializes tests that drive the global <see cref="Time"/> facade and engine threads.</summary>
[CollectionDefinition(EngineHostCollection.Name, DisableParallelization = true)]
public sealed class EngineHostCollection
{
    /// <summary>Collection name shared by engine host tests.</summary>
    public const string Name = "engine host";
}
