using LiteTubeDock.Constants;

namespace LiteTubeDock.Models;

public sealed class AppSettings
{
    public string HomeUrl { get; set; } = AppConstants.DefaultHomeUrl;

    public string LastUrl { get; set; } = AppConstants.DefaultHomeUrl;

    public bool RestoreLastUrl { get; set; } = true;

    public bool AlwaysOnTop { get; set; }

    public bool RestoreWindowState { get; set; } = true;

    public bool EnableAutoplay { get; set; }

    public bool ConfirmNonVideoNavigation { get; set; } = true;

    public bool PlayerModeRefererEnabled { get; set; } = true;

    public string PlayerModeReferer { get; set; } = AppConstants.DefaultPlayerModeRefererValue;

    public bool EnableShortcutKeys { get; set; } = true;

    public bool ShowAddressBar { get; set; } = true;

    public bool ShowNavigationButtons { get; set; } = true;

    public string WebView2UserDataFolder { get; set; } = AppConstants.DefaultWebView2UserDataFolder;

    public string SettingsExportFolder { get; set; } = AppConstants.DefaultSettingsExportFolder;

    public string WindowSizePreset { get; set; } = AppConstants.DefaultWindowSizePreset;

    public WindowSettings Window { get; set; } = new();
}
