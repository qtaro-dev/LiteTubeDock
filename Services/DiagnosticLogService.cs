using System.Diagnostics;
using System.IO;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public static class DiagnosticLogService
{
    private static readonly object SyncRoot = new();
    private static DateOnly? _lastCleanupDate;

    public static string CurrentLogFilePath =>
        Path.Combine(
            AppConstants.LogsDirectoryPath,
            $"{AppConstants.LogFilePrefix}{DateTime.Now:yyyyMMdd}.log");

    public static void LogStartup(IEnumerable<string> args, StartupOptions options)
    {
        Write("Startup", $"Arguments: {FormatArguments(args)}");
        Write("Startup", $"UsePlayerMode: {options.IsPlayerMode}");
        Write("Startup", $"UseIpcEnabled: {options.IsIpcEnabled}");
        Write("Startup", $"StartPaused: {options.StartPaused}");
        Write("Startup", $"KeepMuted: {options.KeepMuted}");
        Write("Startup", $"StartupUrlSpecified: {options.HasInitialUrl}");
        Write("Startup", $"ProcessId: {Environment.ProcessId}");
        Write("Startup", $"AppVersion: {AppConstants.AppVersion}");
    }

    public static void Write(string category, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                CleanupOldLogsIfNeeded();
                Directory.CreateDirectory(AppConstants.LogsDirectoryPath);
                File.AppendAllText(
                    CurrentLogFilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Diagnostic log write failed: {ex.Message}");
        }
    }

    public static void WriteException(string category, string message, Exception exception)
    {
        Write(category, message);
        Write(category, $"ExceptionType: {exception.GetType().Name}");
        Write(category, $"Message: {exception.Message}");
    }

    public static string FormatUrlForLog(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "<invalid-url>";
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.IsNullOrEmpty(uri.Query) ? string.Empty : "...",
            Fragment = string.Empty
        };
        return builder.Uri.ToString();
    }

    private static string FormatArguments(IEnumerable<string> args)
    {
        var values = args.ToArray();
        if (values.Length == 0)
        {
            return "(none)";
        }

        var formatted = new List<string>();
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (value.Equals("--url", StringComparison.OrdinalIgnoreCase))
            {
                formatted.Add(value);
                if (index + 1 < values.Length)
                {
                    formatted.Add("<specified>");
                    index++;
                }

                continue;
            }

            const string urlPrefix = "--url=";
            if (value.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                formatted.Add(urlPrefix + "<specified>");
                continue;
            }

            formatted.Add(value);
        }

        return string.Join(' ', formatted);
    }

    private static void CleanupOldLogsIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_lastCleanupDate == today)
        {
            return;
        }

        _lastCleanupDate = today;
        if (!Directory.Exists(AppConstants.LogsDirectoryPath))
        {
            return;
        }

        var cutoff = DateTime.Now.AddDays(-AppConstants.LogRetentionDays);
        foreach (var file in Directory.GetFiles(AppConstants.LogsDirectoryPath, $"{AppConstants.LogFilePrefix}*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Diagnostic log cleanup failed: {ex.Message}");
            }
        }
    }
}
