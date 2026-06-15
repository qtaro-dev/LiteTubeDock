namespace LiteTubeDock.Services.PlayerAdapters;

public interface IPlayerAdapter
{
    string SiteType { get; }

    string PlayerType { get; }

    bool CanHandle(Uri? uri);
}
