using System;
using System.Collections.Generic;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>统一 GPU 资源句柄：仅携带后端分配的数值标识，无任何资产身份。</summary>
public readonly record struct RenderResourceHandle(ulong Value);

/// <summary>资源创建请求关联标识：Main 域生成、随创建批次进入 Render 域、随结果批次返回。</summary>
/// <param name="Value">请求唯一数值</param>
public readonly record struct RenderResourceRequestId(ulong Value);

/// <summary>资源创建结果状态。</summary>
public enum RenderResourceCreateResultState
{
    /// <summary>创建成功（Handle 有效）。</summary>
    Succeeded,

    /// <summary>创建失败（Error 携带异常；Handle 为 0）。</summary>
    Failed,
}

/// <summary>单个资源创建请求项：RequestId + 无资产语义的创建请求。</summary>
/// <param name="RequestId">关联标识（Main 域回传匹配用）</param>
/// <param name="Request">创建请求（纹理/着色器/网格）</param>
public sealed record RenderResourceCreateItem(RenderResourceRequestId RequestId, RenderResourceCreateRequest Request);

/// <summary>一帧内待创建的资源批次（Main → Render 单向交接，不可变）。</summary>
/// <param name="Items">创建请求项列表</param>
public sealed record RenderResourceCreateBatch(IReadOnlyList<RenderResourceCreateItem> Items)
{
    /// <summary>空批次（无待创建资源）。</summary>
    public static RenderResourceCreateBatch Empty { get; } = new(Array.Empty<RenderResourceCreateItem>());
}

/// <summary>单个资源创建结果：按 RequestId 关联回 Main 域。</summary>
/// <param name="RequestId">关联标识（与创建请求项一致）</param>
/// <param name="State">创建结果状态</param>
/// <param name="Handle">GPU 句柄（失败时为 0）</param>
/// <param name="Error">失败异常（成功时为 null）</param>
/// <param name="Stage">失败所在编译/加载阶段（如 "hlsl-compile"/"gl-specialize"；成功或未知分支为 null）</param>
public sealed record RenderResourceCreateResult(
    RenderResourceRequestId RequestId,
    RenderResourceCreateResultState State,
    RenderResourceHandle Handle,
    Exception? Error,
    string? Stage = null);

/// <summary>一帧内资源创建结果批次（Render → Main 单向交接，不可变）。</summary>
/// <param name="Results">创建结果列表</param>
public sealed record RenderResourceCreateResultBatch(IReadOnlyList<RenderResourceCreateResult> Results)
{
    /// <summary>空结果批次。</summary>
    public static RenderResourceCreateResultBatch Empty { get; } =
        new(Array.Empty<RenderResourceCreateResult>());
}