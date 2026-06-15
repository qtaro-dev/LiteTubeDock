namespace LiteTubeDock.Models;

public sealed class LogFileSnapshot
{
    public string? FilePath { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FolderPath { get; init; } = string.Empty;

    public DateTime? LastWriteTime { get; init; }

    public long FileSizeBytes { get; init; }

    public string Content { get; init; } = string.Empty;

    public bool IsTruncated { get; init; }

    public bool HasLogFile => !string.IsNullOrWhiteSpace(FilePath);
}
