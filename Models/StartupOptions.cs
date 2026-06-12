namespace LiteTubeDock.Models;

public sealed class StartupOptions
{
    public bool IsPlayerMode { get; init; }

    public bool EnableIpc { get; init; }

    public string? InitialUrl { get; init; }

    public bool HasInitialUrl => !string.IsNullOrWhiteSpace(InitialUrl);

    public bool ShowHelp { get; init; }
}
