using System.Runtime.CompilerServices;
using SilkEngine.Core;

namespace SilkEngine.Core.Assets;

/// <summary>异步加载模式</summary>
public enum AsyncLoadMode
{
    /// <summary>调用即登记并调度工作线程加载</summary>
    NormalAsync,

    /// <summary>登记但不调度，首次访问 Asset 时触发（登记不调度；Asset 首次访问时触发加载）</summary>
    LazyAsync,
}

/// <summary>等待者抽象：AssetEntry 以非泛型方式持有各类 AssetRequest</summary>
internal interface IAssetRequest
{
    /// <summary>由主线程帧末调用，填值并唤醒续延</summary>
    void Complete(IAsset? asset, Exception? error);
}

/// <summary>可 await 的资产加载请求（Unity 式自定义 awaitable）</summary>
/// <typeparam name="T">资产类型</typeparam>
public sealed class AssetRequest<T> : INotifyCompletion, IAssetRequest where T : IAsset
{
    private Action? _continuation;
    private T? _asset;

    /// <summary>创建方管理器（LoadAsync 注入）；LazyAsync 触发经此调用，避免全局解析</summary>
    internal AssetManager? Manager { get; set; }

    /// <summary>是否已完成（成功或失败）</summary>
    public bool IsDone { get; internal set; }

    /// <summary>
    /// 加载完成的资产；未完成/失败时为 null
    /// <br/>LazyAsync 模式首次访问触发实际加载调度（触发后重复访问不再触发）
    /// </summary>
    public T? Asset
    {
        get
        {
            Manager?.TriggerLazy(this);
            return _asset;
        }
        internal set => _asset = value;
    }

    /// <summary>加载进度（0~1，完成时为 1）</summary>
    public float Progress { get; internal set; }

    /// <summary>加载失败异常；成功时为 null</summary>
    public Exception? Error { get; internal set; }

    /// <summary>创建已完成请求（缓存命中路径使用）</summary>
    internal static AssetRequest<T> Completed(T asset) =>
        new() { Asset = asset, IsDone = true, Progress = 1f };

    /// <summary>await 机制入口：请求自身即 awaiter</summary>
    public AssetRequest<T> GetAwaiter() => this;

    /// <summary>await 机制：是否无需挂起（LazyAsync 首次检查即触发实际加载调度）</summary>
    public bool IsCompleted
    {
        get
        {
            Manager?.TriggerLazy(this);
            return IsDone;
        }
    }

    /// <summary>await 结果；失败时抛出 Error</summary>
    public T GetResult() => Error is null ? Asset! : throw Error;

    /// <summary>await 机制：登记续延（单请求单续延，等待者合并发生在 AssetEntry 层）</summary>
    public void OnCompleted(Action continuation) => _continuation = continuation;

    /// <summary>主线程帧末调用：填值 → IsDone → 唤醒续延；续延异常捕获并记录，不击穿帧末链路。</summary>
    internal void Complete(T? asset, Exception? error)
    {
        Asset = asset;
        Error = error;
        Progress = 1f;
        IsDone = true;
        try
        {
            _continuation?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error($"[AssetRequest] continuation failed: {ex}");
        }
    }

    void IAssetRequest.Complete(IAsset? asset, Exception? error)
    {
        if (error is null && asset is not T)
            error = new InvalidOperationException(
                $"资产类型不匹配: 期望 {typeof(T).Name}, 实际 {asset?.GetType().Name ?? "null"}");
        Complete(asset is T typed ? typed : default, error);
    }
}
