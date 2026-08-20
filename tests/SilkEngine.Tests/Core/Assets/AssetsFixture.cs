using SilkEngine.Core;
using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>
/// 资产夹具：每测试类新建 AssetManager 实例（ctor 自注册 Services，Material/MeshRenderer 内部调用点经
/// Services.TryGet 解析）；Dispose 按类型注销，避免与并行集合互相清空
/// </summary>
public sealed class AssetsFixture : IDisposable
{
    public AssetManager Manager { get; } = new AssetManager(new RecordingScheduler());

    public void Dispose() => Services.Unregister<AssetManager>();
}
