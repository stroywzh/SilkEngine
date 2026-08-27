using System.Runtime.CompilerServices;
using SilkEngine.Core;

namespace SilkEngine.Assets;

/// <summary>
/// 等待者抽象：AssetEntry 以非泛型方式持有各类 AssetRequest
/// </summary>
internal interface IAssetRequest
{
    /// <summary>由主线程帧末调用，填值并唤醒续延</summary>
    void Complete(object? asset, Exception? error);
}

/// <summary>
/// 旧式可 await 资产加载请求（过渡期兼容表面，由任务 5 删除；新异步模型见 <see cref="AssetOperation{T}"/>）。
/// </summary>
/// <typeparam name="T">资产类型</typeparam>
public sealed class AssetRequest<T> : INotifyCompletion, IAssetRequest
    where T : class
{
    private Action? _continuation;
    private T? _asset;

    /// <summary>创建方管理器（LoadAsync 注入）</summary>
    internal AssetManager? Manager { get; set; }

    /// <summary>是否已完成（成功或失败）</summary>
    public bool IsDone { get; internal set; }

    /// <summary>加载完成的资产；未完成/失败时为 null</summary>
    public T? Asset
    {
        get => _asset;
        internal set => _asset = value;
    }

    /// <summary>加载进度（0~1，完成时为 1）</summary>
    internal float Progress { get; set; }

    /// <summary>加载失败异常；成功时为 null</summary>
    public Exception? Error { get; internal set; }

    /// <summary>创建已完成请求（缓存命中路径使用）</summary>
    internal static AssetRequest<T> Completed(T asset) =>
        new()
        {
            Asset = asset,
            IsDone = true,
            Progress = 1f,
        };

    /// <summary>await 机制入口：请求自身即 awaiter</summary>
    public AssetRequest<T> GetAwaiter() => this;

    /// <summary>await 机制：是否无需挂起</summary>
    public bool IsCompleted => IsDone;

    /// <summary>await 结果；失败时抛出 Error。</summary>
    /// <returns>加载完成的资产实例</returns>
    /// <exception cref="Exception">加载失败（Error 非 null）时抛出加载异常</exception>
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

    void IAssetRequest.Complete(object? asset, Exception? error)
    {
        if (error is null && asset is not T)
            error = new InvalidOperationException(
                $"资产类型不匹配: 期望 {typeof(T).Name}, 实际 {asset?.GetType().Name ?? "null"}"
            );
        Complete(asset is T typed ? typed : default, error);
    }
}
