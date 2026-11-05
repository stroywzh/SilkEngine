using System.Runtime.ExceptionServices;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

internal enum AssetCommandKind : byte
{
    Load = 0,
    Unload = 1,
}

/// <summary>One queued request: a load or an unload, kept in submission order on the static queue.</summary>
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
}

/// <summary>
/// One asynchronous load: the worker decodes it on the thread pool, registers it, and hands a completion back
/// to the main thread.
/// </summary>
internal sealed class AssetLoadRequest
{
    internal AssetLoadRequest(string sourcePath, IAssetLoader loader, CancellationToken cancellationToken)
    {
        SourcePath = sourcePath;
        Loader = loader;
        CancellationToken = cancellationToken;
        Completion = new TaskCompletionSource<IAsset>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal string SourcePath { get; }

    internal IAssetLoader Loader { get; }

    internal CancellationToken CancellationToken { get; }

    internal TaskCompletionSource<IAsset> Completion { get; }
}

/// <summary>Thread-pool result for one request.</summary>
internal readonly struct DecodeResult
{
    internal DecodeResult(AssetLoadRequest request, AssetDraft? draft, Exception? error, bool canceled)
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

/// <summary>Outcome handed to the main-loop pump, which completes the caller's task on the main thread.</summary>
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
