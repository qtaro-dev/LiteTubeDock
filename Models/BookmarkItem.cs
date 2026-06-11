namespace LiteTubeDock.Models;

using LiteTubeDock.Constants;

public sealed class BookmarkItem
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; }

    public string BackgroundColor { get; set; } = AppConstants.DefaultBookmarkBackgroundColor;

    public string ForegroundColor { get; set; } = AppConstants.DefaultBookmarkForegroundColor;

    public bool IsBold { get; set; }

    public string IconPath { get; set; } = string.Empty;

    public string IconShape { get; set; } = AppConstants.DefaultBookmarkIconShape;

    public bool IconRounded { get; set; } = true;

    public string PlaybackMode { get; set; } = AppConstants.DefaultPlaybackMode;

    public bool Autoplay { get; set; }

    public bool Mute { get; set; }

    public bool Loop { get; set; }

    public bool ResumePlayback { get; set; } = true;
}
