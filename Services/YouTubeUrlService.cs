namespace LiteTubeDock.Services;

public static class YouTubeUrlService
{
    public static bool TryGetVideoId(string? url, out string videoId)
    {
        return TryParse(url, out videoId, out _, out _);
    }

    public static bool TryParse(
        string? url,
        out string videoId,
        out string urlType,
        out string failureReason)
    {
        videoId = string.Empty;
        urlType = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            failureReason = "invalid-url";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host == "youtu.be")
        {
            urlType = "youtu.be";
            videoId = parts.FirstOrDefault() ?? string.Empty;
            return ValidateVideoId(videoId, out failureReason);
        }

        if (!IsYouTubeHost(host))
        {
            failureReason = "unsupported-host";
            return false;
        }

        if (uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            urlType = "watch";
            videoId = GetQueryValue(uri.Query, "v");
            return ValidateVideoId(videoId, out failureReason);
        }

        if (parts.Length >= 2
            && parts[0] is var kind
            && IsPathVideoIdType(kind))
        {
            urlType = kind.ToLowerInvariant();
            videoId = parts[1];
            return ValidateVideoId(videoId, out failureReason);
        }

        failureReason = "video-id-not-found";
        return false;
    }

    private static bool IsYouTubeHost(string host)
    {
        return host is "youtube.com"
                or "www.youtube.com"
                or "m.youtube.com"
                or "music.youtube.com"
                or "youtube-nocookie.com"
                or "www.youtube-nocookie.com";
    }

    private static bool IsPathVideoIdType(string value)
    {
        return value.Equals("embed", StringComparison.OrdinalIgnoreCase)
            || value.Equals("shorts", StringComparison.OrdinalIgnoreCase)
            || value.Equals("live", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateVideoId(string value, out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            failureReason = "empty-video-id";
            return false;
        }

        if (value.Length > 64 || value.Any(static c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
        {
            failureReason = "invalid-video-id";
            return false;
        }

        return true;
    }

    private static string GetQueryValue(string query, string key)
    {
        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        return string.Empty;
    }
}
