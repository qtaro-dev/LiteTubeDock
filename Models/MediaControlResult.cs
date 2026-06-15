namespace LiteTubeDock.Models;

public sealed class MediaControlResult
{
    public bool Success { get; init; }

    public bool MediaFound { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string? DocumentReadyState { get; init; }

    public int VideoElementCount { get; init; }

    public int AudioElementCount { get; init; }

    public int IframeElementCount { get; init; }

    public string? TargetElementTag { get; init; }

    public string? CurrentSrc { get; init; }

    public int? ReadyState { get; init; }

    public int? NetworkState { get; init; }

    public int? VideoWidth { get; init; }

    public int? VideoHeight { get; init; }

    public double? DisplayWidth { get; init; }

    public double? DisplayHeight { get; init; }

    public string? Display { get; init; }

    public string? Visibility { get; init; }

    public string? Opacity { get; init; }

    public int AttemptCount { get; init; }

    public string? OperationError { get; init; }

    public bool? IsPaused { get; init; }

    public bool? BeforeMuted { get; init; }

    public bool? AfterMuted { get; init; }

    public bool? IsMuted { get; init; }

    public double? BeforeCurrentTime { get; init; }

    public double? AfterCurrentTime { get; init; }

    public double? CurrentTime { get; init; }

    public double? Duration { get; init; }

    public int DurationMs { get; init; }
}
