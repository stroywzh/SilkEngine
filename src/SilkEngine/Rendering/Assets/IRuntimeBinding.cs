using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Device resource bound to one loaded asset; owned by the runtime table and released on the render thread.</summary>
public interface IRuntimeBinding
{
    /// <summary>Concrete binding type, matching the CLR type used by <c>TryGetBinding&lt;T&gt;</c>.</summary>
    Type BindingType { get; }

    /// <summary>Destroys the device resource; called on the render thread when the binding is released.</summary>
    void Destroy(IRenderDevice device);
}