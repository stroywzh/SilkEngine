namespace SilkEngine.Core.AssetSystem;

/// <summary>One-shot startup work for the asset owner, with completion/failure handoff to the engine host.</summary>
internal sealed class AssetBootstrapHost
{
    private readonly ManualResetEventSlim _completed = new(initialState: true);
    private IAssetBootstrap? _bootstrap;
    private AssetStartupContext _context;
    private Exception? _failure;

    /// <summary>Context the configured bootstrap receives; available after <see cref="Configure"/>.</summary>
    internal AssetStartupContext Context => _context;

    /// <summary>The bootstrap failure, if the last run failed.</summary>
    internal Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Installs the startup work the asset owner runs before it processes any request.</summary>
    internal void Configure(IAssetBootstrap? bootstrap, AssetStartupContext context)
    {
        _context = context;
        Volatile.Write(ref _failure, null);
        Volatile.Write(ref _bootstrap, bootstrap);

        if (bootstrap is null)
        {
            _completed.Set();
            return;
        }

        _completed.Reset();
    }

    /// <summary>Runs the configured bootstrap on the owner thread, recording success or failure.</summary>
    internal void Run()
    {
        IAssetBootstrap? bootstrap = Volatile.Read(ref _bootstrap);
        if (bootstrap is null)
        {
            _completed.Set();
            return;
        }

        try
        {
            bootstrap.Run(_context);
        }
        catch (Exception error)
        {
            Volatile.Write(ref _failure, error);
            Log.Exception(error);
        }
        finally
        {
            _completed.Set();
        }
    }

    /// <summary>Waits (bounded) for the asset owner's bootstrap to signal completion.</summary>
    /// <returns><c>true</c> when the bootstrap completed; <paramref name="failure"/> then carries its error.</returns>
    internal bool Wait(TimeSpan timeout, out Exception? failure)
    {
        bool completed = _completed.Wait(timeout);
        failure = completed ? Volatile.Read(ref _failure) : null;
        return completed;
    }
}
