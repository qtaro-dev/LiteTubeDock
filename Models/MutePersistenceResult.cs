namespace LiteTubeDock.Models;

public sealed class MutePersistenceResult
{
    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string? CurrentUrl { get; init; }

    public bool MutePersistenceEnabled { get; init; }

    public bool? DesiredMutedState { get; init; }

    public bool? ActualMutedState { get; init; }

    public bool? ActualMutedStateBefore { get; init; }

    public bool? ActualMutedStateAfter { get; init; }

    public int MediaElementCount { get; init; }

    public bool MediaElementChanged { get; init; }

    public string? LastMuteReapplyReason { get; init; }

    public string? ReapplyReason { get; init; }

    public string? ReapplyResult { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
