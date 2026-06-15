namespace LiteTubeDock.Models;

public sealed class UnifiedPlayerStateResult
{
    public bool Success { get; init; }

    public bool MediaFound { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string SiteType { get; init; } = string.Empty;

    public string PlayerType { get; init; } = string.Empty;

    public string? CurrentUrl { get; init; }

    public string? MediaIdentity { get; init; }

    public int MediaRevision { get; init; }

    public string? Title { get; init; }

    public double? CurrentTimeSeconds { get; init; }

    public double? DurationSeconds { get; init; }

    public bool? IsPlaying { get; init; }

    public bool? IsPaused { get; init; }

    public bool? IsEnded { get; init; }

    public bool IsSeekable { get; init; }

    public bool IsLive { get; init; }

    public bool IsAdvertisement { get; init; }

    public int SeekableRangeCount { get; init; }

    public double? SeekableStartSeconds { get; init; }

    public double? SeekableEndSeconds { get; init; }

    public int? VolumePercent { get; init; }

    public bool? IsMuted { get; init; }

    public int? DesiredVolumePercent { get; init; }

    public bool? DesiredMutedState { get; init; }

    public bool ControlPolicyEnabled { get; init; }

    public int ControlPolicyExpiresInSeconds { get; init; }

    public bool CanGoNext { get; init; }

    public bool CanGoPrevious { get; init; }

    public bool CanGoNextChapter { get; init; }

    public bool CanGoPreviousChapter { get; init; }

    public string? CurrentChapter { get; init; }

    public int ChapterCount { get; init; }

    public string? EndedReason { get; init; }

    public string? Operation { get; init; }

    public string? OperationResult { get; init; }

    public double? RequestedPositionSeconds { get; init; }

    public int? RequestedVolumePercent { get; init; }

    public bool? RequestedMuted { get; init; }

    public int MediaElementCount { get; init; }

    public string? TargetElementTag { get; init; }

    public string? CurrentSrc { get; init; }

    public int? ReadyState { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
