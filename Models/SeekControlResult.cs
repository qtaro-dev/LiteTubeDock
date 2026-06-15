namespace LiteTubeDock.Models;

public sealed class SeekControlResult
{
    public bool Success { get; init; }

    public bool MediaFound { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public double? RequestedPositionSeconds { get; init; }

    public double? ActualPositionSeconds { get; init; }

    public double? Duration { get; init; }

    public double? DurationSeconds { get; init; }

    public bool IsSeekable { get; init; }

    public bool IsLive { get; init; }

    public string? CurrentUrl { get; init; }

    public string? MediaTitle { get; init; }

    public string? MediaIdentity { get; init; }

    public int MediaRevision { get; init; }

    public int MediaElementCount { get; init; }

    public string? TargetElementTag { get; init; }

    public string? CurrentSrc { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
