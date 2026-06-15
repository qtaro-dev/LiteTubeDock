namespace LiteTubeDock.Services.PlayerAdapters;

public sealed class GenericMediaPlayerAdapter : IPlayerAdapter
{
    public string SiteType => "generic";

    public string PlayerType => "html5-media";

    public bool CanHandle(Uri? uri)
    {
        return true;
    }
}
