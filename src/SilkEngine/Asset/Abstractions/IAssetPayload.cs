namespace SilkEngine.Asset.Abstractions;

/// <summary>Decoded payload handed to the data plane; disposing it releases the CPU copy once nothing else reads it.</summary>
internal interface IAssetPayload : IDisposable { }
