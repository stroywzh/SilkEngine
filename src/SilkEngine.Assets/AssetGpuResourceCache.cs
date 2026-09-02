using System.Collections.Generic;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Assets;

/// <summary>
/// GPU 句柄缓存（Assets 侧）：只保存 (AssetId, Revision, Kind) → RenderHandle 的关联，
/// 驱逐时生成无资产语义的 <see cref="RenderResourceReleaseRequest"/>。
/// Rendering 侧只消费 request/handle，不能从 Handle 反查资产。
/// </summary>
public sealed class AssetGpuResourceCache
{
    private readonly Dictionary<(AssetId AssetId, ulong Revision, RenderResourceKind Kind), ulong> _handles = new();
    private readonly Dictionary<RenderResourceRequestId, TrackedRenderRequest> _requests = new();

    /// <summary>创建失败结果账本（RequestId → 阶段 + 消息）：ApplyCreateResults 失败分支落账，诊断/测试查询用。</summary>
    private readonly Dictionary<RenderResourceRequestId, (string Stage, string Message)> _failures = new();

    /// <summary>登记创建请求关联（Main 域调用）：RequestId → (AssetId, Revision, Kind)。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <param name="kind">资源种类</param>
    public void TrackRequest(RenderResourceRequestId requestId, AssetId assetId, ulong revision, RenderResourceKind kind)
        => _requests[requestId] = new TrackedRenderRequest(assetId, revision, kind, requestId);

    /// <summary>按 RequestId 解析创建请求关联（Main 域调用；结果批次回传后匹配资产身份）。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <param name="tracked">关联记录（未命中为 null）</param>
    /// <returns>命中为 true</returns>
    internal bool TryResolveRequest(RenderResourceRequestId requestId, out TrackedRenderRequest? tracked)
        => _requests.TryGetValue(requestId, out tracked);

    /// <summary>移除创建请求关联（Main 域调用；结果已应用或请求取消时调用）。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <returns>存在并移除为 true</returns>
    public bool RemoveRequest(RenderResourceRequestId requestId)
        => _requests.Remove(requestId);

    /// <summary>登记纹理 GPU 句柄（渲染侧创建完成后回填）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订（与构建键一致；防止过期句柄误释放）</param>
    /// <param name="handle">渲染侧纹理句柄</param>
    public void Publish(AssetId assetId, ulong revision, RenderTextureHandle handle)
        => Publish(assetId, revision, RenderResourceKind.Texture, handle.Value);

    /// <summary>登记网格 GPU 句柄（渲染侧创建完成后回填）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <param name="handle">渲染侧网格句柄</param>
    public void Publish(AssetId assetId, ulong revision, RenderMeshHandle handle)
        => Publish(assetId, revision, RenderResourceKind.Mesh, handle.Value);

    /// <summary>登记着色器 GPU 句柄（渲染侧创建完成后回填）</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <param name="handle">渲染侧着色器句柄</param>
    public void Publish(AssetId assetId, ulong revision, RenderShaderHandle handle)
        => Publish(assetId, revision, RenderResourceKind.Shader, handle.Value);

    /// <summary>按 (AssetId, Revision, Kind) 查询句柄；未登记返回 false。</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <param name="kind">资源种类</param>
    /// <param name="handle">已登记的句柄（未登记为 0）</param>
    /// <returns>查询命中为 true</returns>
    public bool TryGet(AssetId assetId, ulong revision, RenderResourceKind kind, out ulong handle)
        => _handles.TryGetValue((assetId, revision, kind), out handle);

    /// <summary>驱逐指定 (AssetId, Revision) 的纹理句柄并生成释放请求；未登记返回零句柄 no-op 请求。</summary>
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

    /// <summary>驱逐指定 (AssetId, Revision) 全部种类的 GPU 句柄并生成释放请求列表；未登记返回空列表。</summary>
    /// <param name="assetId">资产标识</param>
    /// <param name="revision">源修订</param>
    /// <returns>释放请求列表（无资产语义：种类 + 句柄）</returns>
    public IReadOnlyList<RenderResourceReleaseRequest> EvictAll(AssetId assetId, ulong revision)
    {
        var releases = new List<RenderResourceReleaseRequest>(3);
        foreach (var kind in new[] { RenderResourceKind.Texture, RenderResourceKind.Shader, RenderResourceKind.Mesh })
        {
            if (_handles.Remove((assetId, revision, kind), out var handle))
                releases.Add(new RenderResourceReleaseRequest(kind, handle));
        }
        return releases;
    }

    /// <summary>记录创建/编译失败（Main 域 ApplyCreateResults 失败分支调用）：阶段信息与错误消息按 RequestId 留存。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <param name="stage">失败阶段（如 "hlsl-compile"/"gl-specialize"）</param>
    /// <param name="message">失败详情（含 source path/入口/profile/backend 上下文）</param>
    public void RecordFailure(RenderResourceRequestId requestId, string stage, string message)
        => _failures[requestId] = (stage, message);

    /// <summary>按 RequestId 查询失败阶段与消息（测试/诊断用；未记录返回 false）。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <param name="stage">失败阶段（未记录为 null）</param>
    /// <param name="message">失败详情（未记录为 null）</param>
    /// <returns>命中为 true</returns>
    internal bool TryGetFailure(RenderResourceRequestId requestId, out string? stage, out string? message)
    {
        if (_failures.TryGetValue(requestId, out var failure))
        {
            stage = failure.Stage;
            message = failure.Message;
            return true;
        }
        stage = null;
        message = null;
        return false;
    }

    /// <summary>移除失败记录（测试/清理用；失败账本仅在显式调用时清除）。</summary>
    /// <param name="requestId">创建请求关联标识</param>
    /// <returns>存在并移除为 true</returns>
    public bool RemoveFailure(RenderResourceRequestId requestId)
        => _failures.Remove(requestId);

    private void Publish(AssetId assetId, ulong revision, RenderResourceKind kind, ulong handle)
        => _handles[(assetId, revision, kind)] = handle;
}

/// <summary>创建请求关联记录（Assets 侧内部）：把无资产语义的 RequestId 关联回资产身份。</summary>
/// <param name="AssetId">资产标识</param>
/// <param name="Revision">源修订</param>
/// <param name="Kind">资源种类</param>
/// <param name="RequestId">创建请求关联标识</param>
internal sealed record TrackedRenderRequest(
    AssetId AssetId,
    ulong Revision,
    RenderResourceKind Kind,
    RenderResourceRequestId RequestId);
