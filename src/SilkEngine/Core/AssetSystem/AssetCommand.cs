using System.Runtime.ExceptionServices;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

internal enum AssetCommandKind : byte
{
    Load = 0,
    Unload = 1,
    ReleaseRenderRef = 2,
}

/// <summary>One queued owner-thread command: a load, a cache-pin release or a render-reference release.</summary>
internal readonly struct AssetCommand
{
    private AssetCommand(AssetCommandKind kind, AssetLoadRequest? request, AssetId assetId)
    {
        Kind = kind;
        Request = request;
        AssetId = assetId;
    }

    internal AssetCommandKind Kind { get; }

    internal AssetLoadRequest? Request { get; }

    internal AssetId AssetId { get; }

    internal static AssetCommand Load(AssetLoadRequest request) =>
        new(AssetCommandKind.Load, request, AssetId.Invalid);

    internal static AssetCommand Unload(AssetId assetId) =>
        new(AssetCommandKind.Unload, null, assetId);

    internal static AssetCommand ReleaseRenderRef(AssetId assetId) =>
        new(AssetCommandKind.ReleaseRenderRef, null, assetId);
}

/// <summary>One asynchronous or blocking load request; completion handling depends on <see cref="Blocking"/>.</summary>
internal sealed class AssetLoadRequest
{
    private int _abandoned;

    internal AssetLoadRequest(
        AssetSource source,
        IAssetLoader loader,
        CancellationToken cancellationToken,
        bool blocking
    )
    {
        Source = source;
        Loader = loader;
        CancellationToken = cancellationToken;
        Blocking = blocking;
        Completion = new TaskCompletionSource<IAsset>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    }

    internal AssetSource Source { get; }

    internal string SourcePath => Source.ProjectPath;

    internal IAssetLoader Loader { get; }

    internal CancellationToken CancellationToken { get; }

    /// <summary>True when the caller blocks; the owner thread then completes the task directly.</summary>
    internal bool Blocking { get; }

    /// <summary>Set by shutdown for requests that must not be registered any more.</summary>
    internal bool Abandoned
    {
        get => Volatile.Read(ref _abandoned) != 0;
        set => Volatile.Write(ref _abandoned, value ? 1 : 0);
    }

    internal TaskCompletionSource<IAsset> Completion { get; }
}

/// <summary>Thread-pool decode result.</summary>
internal readonly struct DecodeResult
{
    internal DecodeResult(
        AssetLoadRequest request,
        AssetDraft? draft,
        Exception? error,
        bool canceled
    )
    {
        Request = request;
        Draft = draft;
        Error = error;
        Canceled = canceled;
    }

    internal AssetLoadRequest Request { get; }

    internal AssetDraft? Draft { get; }

    internal Exception? Error { get; }

    internal bool Canceled { get; }
}

/// <summary>Outcome handed to the main-loop pump for async callers.</summary>
internal readonly struct AssetCompletion
{
    private AssetCompletion(
        AssetLoadRequest request,
        IAsset? asset,
        ExceptionDispatchInfo? failure,
        bool canceled
    )
    {
        Request = request;
        Asset = asset;
        Failure = failure;
        IsCanceled = canceled;
    }

    internal AssetLoadRequest Request { get; }

    internal IAsset? Asset { get; }

    internal ExceptionDispatchInfo? Failure { get; }

    internal bool IsCanceled { get; }

    internal static AssetCompletion Loaded(AssetLoadRequest request, IAsset asset) =>
        new(request, asset, null, false);

    internal static AssetCompletion Failed(AssetLoadRequest request, Exception error) =>
        new(request, null, ExceptionDispatchInfo.Capture(error), false);

    internal static AssetCompletion Canceled(AssetLoadRequest request) =>
        new(request, null, null, true);
}
