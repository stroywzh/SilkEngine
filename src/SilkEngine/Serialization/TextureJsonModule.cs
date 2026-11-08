using System.Runtime.CompilerServices;

namespace SilkEngine.Serialization;

/// <summary>Registers the texture content converter before any document is serialized.</summary>
internal static class TextureJsonModule
{
    [ModuleInitializer]
    internal static void Initialize() => JsonConverterRegistry.Register(new TextureDataJsonConverter());
}
