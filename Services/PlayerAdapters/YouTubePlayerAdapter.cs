namespace LiteTubeDock.Services.PlayerAdapters;

public sealed class YouTubePlayerAdapter : IPlayerAdapter
{
    public string SiteType => "youtube";

    public string PlayerType => "youtube-html5";

    public bool CanHandle(Uri? uri)
    {
        return uri?.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) == true
            || uri?.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase) == true
            || uri?.Host.EndsWith("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) == true;
    }
}
