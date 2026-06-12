namespace LiteTubeDock.Models;

public sealed class IpcResponse
{
    public bool Success { get; init; }

    public string? Command { get; init; }

    public int ProcessId { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }

    public object? Data { get; init; }
}
