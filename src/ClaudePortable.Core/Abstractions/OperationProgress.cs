namespace ClaudePortable.Core.Abstractions;

/// <summary>
/// Progress report emitted by long-running BackupEngine / RestoreEngine
/// operations. Consumers typically wrap this in a <see cref="Progress{T}"/>
/// to bridge the threadpool -> UI-thread hop.
/// </summary>
/// <param name="Phase">Short human-readable phase label, e.g. "Extracting archive".</param>
/// <param name="Current">Items completed so far, or null for indeterminate phases.</param>
/// <param name="Total">Items to do, or null for indeterminate phases.</param>
public sealed record OperationProgress(string Phase, int? Current = null, int? Total = null)
{
    public double? Fraction => (Current.HasValue && Total is > 0)
        ? Math.Clamp((double)Current.Value / Total.Value, 0, 1)
        : null;

    public bool IsIndeterminate => !Fraction.HasValue;
}
