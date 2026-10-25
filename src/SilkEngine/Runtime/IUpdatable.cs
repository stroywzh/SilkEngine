namespace SilkEngine.Runtime;

/// <summary>
/// Internal per-frame driver contract. Implemented by managers and other engine-internal objects.
/// </summary>
/// <remarks>Member names are the object-level dispatch names and were not renamed in this batch.</remarks>
internal interface IUpdatable
{
    /// <summary>Runs one update step.</summary>
    void UpdateInternal();

    /// <summary>Runs one fixed-step update.</summary>
    void FixedUpdateInternal();

    /// <summary>Runs one late-update step.Called after rendering in this frame.</summary>
    void LateUpdateInternal();
}
