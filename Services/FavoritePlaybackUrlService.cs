using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public static class FavoritePlaybackUrlService
{
    public static string GetNavigationUrl(BookmarkItem bookmark)
    {
        if (bookmark.PlaybackMode != AppConstants.PlayerPlaybackMode)
        {
            return bookmark.ResumePlayback
                ? bookmark.Url
                : RemoveStartParameters(bookmark.Url);
        }

        if (!TryGetYouTubeVideoId(bookmark.Url, out var videoId, out var startSeconds))
        {
            return bookmark.ResumePlayback
                ? bookmark.Url
                : RemoveStartParameters(bookmark.Url);
        }

        var builder = new UriBuilder($"https://www.youtube.com/embed/{Uri.EscapeDataString(videoId)}");
        var query = new List<string>();

        if (bookmark.Autoplay)
        {
            query.Add("autoplay=1");
        }

        if (bookmark.Mute)
        {
            query.Add("mute=1");
        }

        if (bookmark.Loop)
        {
            query.Add("loop=1");
            query.Add("playlist=" + Uri.EscapeDataString(videoId));
        }

        if (bookmark.ResumePlayback && startSeconds > 0)
        {
            query.Add("start=" + startSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.Query = string.Join("&", query);
        return builder.Uri.ToString();
    }

    private static bool TryGetYouTubeVideoId(string? url, out string videoId, out int startSeconds)
    {
        videoId = string.Empty;
        startSeconds = 0;

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        startSeconds = GetStartSeconds(uri.Query);

        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be")
        {
            videoId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return IsValidVideoId(videoId);
        }

        if (host is not ("www.youtube.com" or "youtube.com" or "m.youtube.com"))
        {
            return false;
        }

        if (uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            videoId = GetQueryValue(uri.Query, "v");
            return IsValidVideoId(videoId);
        }

        var pathParts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length >= 2 && pathParts[0].Equals("embed", StringComparison.OrdinalIgnoreCase))
        {
            videoId = pathParts[1];
            return IsValidVideoId(videoId);
        }

        return false;
    }

    private static string RemoveStartParameters(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url ?? string.Empty;
        }

        var pairs = GetQueryPairs(uri.Query)
            .Where(pair => !IsStartParameter(pair.Key))
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))
            .ToList();

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", pairs)
        };

        if (IsStartParameter(uri.Fragment.TrimStart('#').Split('=', 2).FirstOrDefault() ?? string.Empty))
        {
            builder.Fragment = string.Empty;
        }

        return builder.Uri.ToString();
    }

    private static string GetQueryValue(string query, string key)
    {
        foreach (var pair in GetQueryPairs(query))
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return string.Empty;
    }

    private static int GetStartSeconds(string query)
    {
        var start = GetQueryValue(query, "start");
        if (int.TryParse(start, out var startSeconds) && startSeconds > 0)
        {
            return startSeconds;
        }

        return TryParseYouTubeTime(GetQueryValue(query, "t"), out var seconds)
            ? seconds
            : 0;
    }

    private static IReadOnlyList<(string Key, string Value)> GetQueryPairs(string query)
    {
        var trimmed = query.TrimStart('?');
        return trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .Select(pair => (Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1])))
            .ToList();
    }

    private static bool IsStartParameter(string key)
    {
        return key.Equals("t", StringComparison.OrdinalIgnoreCase)
            || key.Equals("start", StringComparison.OrdinalIgnoreCase)
            || key.Equals("time_continue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseYouTubeTime(string value, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var total = 0;
        var number = string.Empty;
        foreach (var c in value.Trim())
        {
            if (char.IsDigit(c))
            {
                number += c;
                continue;
            }

            if (number.Length == 0)
            {
                return false;
            }

            var parsed = int.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
            total += c switch
            {
                'h' or 'H' => parsed * 3600,
                'm' or 'M' => parsed * 60,
                's' or 'S' => parsed,
                _ => 0
            };
            number = string.Empty;
        }

        if (number.Length > 0)
        {
            total += int.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
        }

        seconds = total;
        return seconds > 0;
    }

    private static bool IsValidVideoId(string value)
    {
        return value.Length > 0
            && value.Length <= 64
            && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }
}
