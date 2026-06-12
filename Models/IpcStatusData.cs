namespace LiteTubeDock.Models;

public sealed class IpcStatusData
{
    public int ProcessId { get; init; }

    public string WindowTitle { get; init; } = string.Empty;

    public bool IsPlayerMode { get; init; }

    public string CurrentUrl { get; init; } = string.Empty;

    public bool IsWebViewReady { get; init; }

    public string AppVersion { get; init; } = string.Empty;
}
