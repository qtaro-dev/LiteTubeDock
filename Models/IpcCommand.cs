using System.Text.Json;

namespace LiteTubeDock.Models;

public sealed class IpcCommand
{
    public string? Command { get; init; }

    public string? Url { get; init; }

    public bool? Enabled { get; init; }

    public bool? MutePersistenceEnabled { get; init; }

    public bool? Value { get; init; }

    public JsonElement? VolumePercent { get; init; }

    public JsonElement? Muted { get; init; }

    public JsonElement? PositionSeconds { get; init; }

    public JsonElement? ExpirationSeconds { get; init; }

    public string? RequestId { get; init; }
}
