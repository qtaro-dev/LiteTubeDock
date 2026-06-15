namespace LiteTubeDock.Models;

public sealed class UnifiedPlayerControlPolicy
{
    public bool Enabled { get; init; }

    public int? DesiredVolumePercent { get; init; }

    public bool? DesiredMutedState { get; init; }

    public int ExpirationSeconds { get; init; }
}
