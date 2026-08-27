namespace SilkEngine.Assets;

public abstract class Asset : IAsset
{
    public Guid Guid { get; set; }
    public string Name { get; set; }
}
