namespace LiteTubeDock.Models;

public sealed class IpcCommand
{
    public string? Command { get; init; }

    public string? Url { get; init; }

    public string? RequestId { get; init; }
}
