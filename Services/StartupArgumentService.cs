using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public static class StartupArgumentService
{
    public static StartupOptions Parse(IEnumerable<string> args)
    {
        var isPlayerMode = false;
        var enableIpc = false;
        var showHelp = false;
        string? initialUrl = null;

        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (arg.Equals("--player-mode", StringComparison.OrdinalIgnoreCase))
            {
                isPlayerMode = true;
                continue;
            }

            if (arg.Equals("--ipc-enabled", StringComparison.OrdinalIgnoreCase))
            {
                enableIpc = true;
                continue;
            }

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                continue;
            }

            if (arg.Equals("--url", StringComparison.OrdinalIgnoreCase))
            {
                if (enumerator.MoveNext())
                {
                    initialUrl = NormalizeUrl(enumerator.Current);
                }

                continue;
            }

            const string urlPrefix = "--url=";
            if (arg.StartsWith(urlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                initialUrl = NormalizeUrl(arg[urlPrefix.Length..]);
            }
        }

        return new StartupOptions
        {
            IsPlayerMode = isPlayerMode,
            EnableIpc = enableIpc,
            InitialUrl = initialUrl,
            ShowHelp = showHelp
        };
    }

    private static string? NormalizeUrl(string? value)
    {
        var candidate = value?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && !string.IsNullOrWhiteSpace(uri.Host)
                ? uri.ToString()
                : null;
    }
}
