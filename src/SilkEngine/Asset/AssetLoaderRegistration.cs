using SilkEngine.Asset.Internal;

namespace SilkEngine.Asset;

/// <summary>启动前注册引擎内置资产加载器。</summary>
/// <remarks><see cref="Application.AssetLoaders"/> 在第一次解析加载器时被冻结，因此必须在启动前调用。</remarks>
internal static class AssetLoaderRegistration
{
    /// <summary>注册内置加载器：纹理类型映射到运行时纹理加载器。</summary>
    public static void RegisterDefaults()
    {
        Application.AssetLoaders[typeof(Texture)] = new TextureAssetLoader();
    }
}

