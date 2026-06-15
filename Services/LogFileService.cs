using System.Diagnostics;
using System.IO;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public sealed class LogFileService
{
    private const long MaxDisplayBytes = 2 * 1024 * 1024;

    public string LogsDirectoryPath => AppConstants.LogsDirectoryPath;

    public async Task<LogFileSnapshot> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var file = GetLatestLogFile();
        return file is null
            ? CreateEmptySnapshot()
            : await LoadAsync(file.FullName, cancellationToken);
    }

    public async Task<LogFileSnapshot> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return CreateEmptySnapshot();
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(filePath);
            var isTruncated = info.Length > MaxDisplayBytes;
            var content = ReadContent(info, isTruncated);
            return new LogFileSnapshot
            {
                FilePath = info.FullName,
                FileName = info.Name,
                FolderPath = info.DirectoryName ?? AppConstants.LogsDirectoryPath,
                LastWriteTime = info.LastWriteTime,
                FileSizeBytes = info.Length,
                Content = content,
                IsTruncated = isTruncated
            };
        }, cancellationToken);
    }

    public void OpenLogsFolder()
    {
        Directory.CreateDirectory(AppConstants.LogsDirectoryPath);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppConstants.LogsDirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("LogViewer", "Open logs folder failed.", ex);
            throw;
        }
    }

    private static LogFileSnapshot CreateEmptySnapshot()
    {
        return new LogFileSnapshot
        {
            FolderPath = AppConstants.LogsDirectoryPath,
            Content = AppConstants.LogViewerNoLogFileText
        };
    }

    private static FileInfo? GetLatestLogFile()
    {
        if (!Directory.Exists(AppConstants.LogsDirectoryPath))
        {
            return null;
        }

        var files = Directory.GetFiles(AppConstants.LogsDirectoryPath, $"{AppConstants.LogFilePrefix}*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(GetLogDateScore)
            .ThenByDescending(file => file.LastWriteTime)
            .ToArray();

        var todayName = $"{AppConstants.LogFilePrefix}{DateTime.Now:yyyyMMdd}.log";
        return files.FirstOrDefault(file => file.Name.Equals(todayName, StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault();
    }

    private static DateTime GetLogDateScore(FileInfo file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Name);
        var prefix = AppConstants.LogFilePrefix;
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParseExact(
                name[prefix.Length..],
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        return DateTime.MinValue;
    }

    private static string ReadContent(FileInfo info, bool tailOnly)
    {
        using var stream = new FileStream(
            info.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (tailOnly)
        {
            stream.Seek(-MaxDisplayBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return tailOnly
            ? AppConstants.LogViewerTruncatedText + Environment.NewLine + Environment.NewLine + content
            : content;
    }
}
