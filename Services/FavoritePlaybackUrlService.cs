using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public static class FavoritePlaybackUrlService
{
    public static string GetNavigationUrl(BookmarkItem bookmark)
    {
        return bookmark.Url;
    }

    public static bool TryGetYouTubeVideoId(string? url, out string videoId)
    {
        return YouTubeUrlService.TryGetVideoId(url, out videoId);
    }
}
