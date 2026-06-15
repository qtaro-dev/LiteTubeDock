namespace LiteTubeDock.Models;

public sealed class IpcStatusData
{
    public int ProcessId { get; init; }

    public string WindowTitle { get; init; } = string.Empty;

    public bool IsPlayerMode { get; init; }

    public bool IsIpcEnabled { get; init; }

    public bool IpcEnabled { get; init; }

    public bool PipeServerStarted { get; init; }

    public string PipeName { get; init; } = string.Empty;

    public string? LastIpcError { get; init; }

    public int MediaElementCount { get; init; }

    public int VideoElementCount { get; init; }

    public int AudioElementCount { get; init; }

    public string DocumentReadyState { get; init; } = string.Empty;

    public string LastMediaCommand { get; init; } = string.Empty;

    public string LastMediaCommandResult { get; init; } = string.Empty;

    public string LastMediaErrorCode { get; init; } = string.Empty;

    public string CurrentUrl { get; init; } = string.Empty;

    public bool IsWebViewReady { get; init; }

    public bool IsInlineFullscreen { get; init; }

    public bool InlineFullscreenStateKnown { get; init; }

    public string InlineFullscreenErrorCode { get; init; } = string.Empty;

    public bool MutePersistenceEnabled { get; init; }

    public int? DesiredVolumePercent { get; init; }

    public bool? DesiredMutedState { get; init; }

    public bool? ActualMutedState { get; init; }

    public string LastMuteReapplyReason { get; init; } = string.Empty;

    public string AppVersion { get; init; } = string.Empty;
}
