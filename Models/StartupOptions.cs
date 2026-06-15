namespace LiteTubeDock.Models;

public sealed class StartupOptions
{
    public bool IsPlayerMode { get; init; }

    public bool IsIpcEnabled { get; init; }

    public bool StartPaused { get; init; }

    public bool KeepMuted { get; init; }

    public string? InitialUrl { get; init; }

    public bool HasInitialUrl => !string.IsNullOrWhiteSpace(InitialUrl);

    public bool ShowHelp { get; init; }
}
