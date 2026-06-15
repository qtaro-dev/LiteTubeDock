namespace LiteTubeDock.Models;

public sealed class InlineFullscreenResult
{
    public bool Success { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public string? CurrentUrl { get; init; }

    public bool YoutubeDetected { get; init; }

    public bool IsShorts { get; init; }

    public bool? InlineFullscreenBefore { get; init; }

    public bool? InlineFullscreenAfter { get; init; }

    public bool? IsInlineFullscreen { get; init; }

    public string? DomOperationResult { get; init; }

    public string? FullscreenApiResult { get; init; }

    public string? OperationError { get; init; }

    public int DurationMs { get; init; }
}
