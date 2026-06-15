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

    public bool Autoplay { get; set; }

    public bool Mute { get; set; }

    public bool Loop { get; set; }

    public int StartPositionSeconds { get; set; }
}
