namespace SilkEngine.Core.Assets;

/// <summary>引用归零时需级联处理的资产（Material 主纹理归还等）；AssetManager 经此回调，避免依赖具体类型。</summary>
internal interface IReleaseAwareAsset
{
    void OnAssetReleased(AssetManager manager);
}
