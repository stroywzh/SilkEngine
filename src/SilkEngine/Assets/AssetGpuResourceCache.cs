using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Assets;

/// <summary>
/// GPU 句柄缓存（Assets 侧）：只保存 (AssetId, Revision) → RenderHandle 的关联，
/// 驱逐时生成无资产语义的 <see cref="RenderResourceReleaseRequest"/>。
/// Rendering 侧只消费 request/handle，不能从 Handle 反查资产。
/// </summary>
public sealed class AssetGpuResourceCache
{
    private readonly Dictionary<(AssetId AssetId, ulong Revision, RenderResourceKind Kind), ulong> _handles = new();

    /// <summary>登记纹理 GPU 句柄（渲染侧创建完成后回填）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订（与构建键一致；防止过期句柄误释放）</param>
    /// <param name="handle">渲染侧纹理句柄</param>
    public void Publish(AssetId assetId, ulong revision, RenderTextureHandle handle)
        => _handles[(assetId, revision, RenderResourceKind.Texture)] = handle.Value;

    /// <summary>驱逐指定 (AssetId, Revision) 的 GPU 句柄并生成释放请求；未登记返回零句柄 no-op 请求。</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <returns>释放请求（未登记时 Handle 为 0，消费方跳过）</returns>
    public RenderResourceReleaseRequest Evict(AssetId assetId, ulong revision)
    {
        var key = (assetId, revision, RenderResourceKind.Texture);
        if (!_handles.Remove(key, out var handle))
            return new RenderResourceReleaseRequest(RenderResourceKind.Texture, 0);
        return new RenderResourceReleaseRequest(RenderResourceKind.Texture, handle);
    }
}
