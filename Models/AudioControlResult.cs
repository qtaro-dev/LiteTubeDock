namespace LiteTubeDock.Models;

public sealed class AudioControlResult
{
    public bool Success { get; init; }

    public bool MediaFound { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string? CurrentUrl { get; init; }

    public string? MediaTitle { get; init; }

    public double? Volume { get; init; }

    public int? VolumePercent { get; init; }

    public int? RequestedVolumePercent { get; init; }

    public double? AppliedVolume { get; init; }

    public int? AppliedVolumePercent { get; init; }

    public int? DesiredVolumePercent { get; init; }

    public bool? IsMuted { get; init; }

    public bool? RequestedMuted { get; init; }

    public bool? ActualMuted { get; init; }

    public bool MutePersistenceEnabled { get; init; }

    public bool? DesiredMutedState { get; init; }

    public int MediaElementCount { get; init; }

    public int VideoElementCount { get; init; }

    public int AudioElementCount { get; init; }

    public bool? IsPlaying { get; init; }

    public double? CurrentTime { get; init; }

    public double? CurrentTimeSeconds { get; init; }

    public double? Duration { get; init; }

    public double? DurationSeconds { get; init; }

    public bool IsSeekable { get; init; }

    public bool IsLive { get; init; }

    public double? PlaybackRate { get; init; }

    public bool? IsPaused { get; init; }

    public string? MediaIdentity { get; init; }

    public int MediaRevision { get; init; }

    public string? TargetElementTag { get; init; }

    public string? CurrentSrc { get; init; }

    public int? ReadyState { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
