namespace LiteTubeDock.Services.PlayerAdapters;

public sealed class TwitchPlayerAdapter : IPlayerAdapter
{
    public string SiteType => "twitch";

    public string PlayerType => "twitch-html5";

    public bool CanHandle(Uri? uri)
    {
        return uri?.Host.EndsWith("twitch.tv", StringComparison.OrdinalIgnoreCase) == true;
    }
}
