namespace LiteTubeDock.Models;

public sealed class AudioPersistenceResult
{
    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string? CurrentUrl { get; init; }

    public int? DesiredVolumePercent { get; init; }

    public int? ActualVolumePercent { get; init; }

    public bool? DesiredMutedState { get; init; }

    public bool? ActualMutedState { get; init; }

    public int MediaElementCount { get; init; }

    public bool MediaElementChanged { get; init; }

    public string? MediaIdentity { get; init; }

    public int MediaRevision { get; init; }

    public string? TargetElementTag { get; init; }

    public string? CurrentSrc { get; init; }

    public int? ReadyState { get; init; }

    public string? ReapplyReason { get; init; }

    public string? ReapplyResult { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
