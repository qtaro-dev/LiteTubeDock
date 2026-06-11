using System.IO;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public sealed class SettingsService : JsonFileService
{
    private readonly string _settingsFilePath;

    public SettingsService()
        : this(AppConstants.SettingsFilePath)
    {
    }

    public SettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public AppSettings Load()
    {
        EnsureDirectory(_settingsFilePath);

        if (!File.Exists(_settingsFilePath))
        {
            var settings = CreateDefault();
            Save(settings);
            return settings;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings);
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
        catch (IOException)
        {
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        EnsureDirectory(_settingsFilePath);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    public AppSettings CreateDefault()
    {
        return new AppSettings
        {
            HomeUrl = AppConstants.DefaultHomeUrl,
            LastUrl = AppConstants.DefaultHomeUrl,
            RestoreLastUrl = true,
            AlwaysOnTop = false,
            RestoreWindowState = true,
            EnableAutoplay = false,
            ConfirmNonVideoNavigation = true,
            PlayerModeRefererEnabled = true,
            PlayerModeReferer = AppConstants.DefaultPlayerModeRefererValue,
            EnableShortcutKeys = true,
            ShowAddressBar = true,
            ShowNavigationButtons = true,
            WebView2UserDataFolder = AppConstants.WebView2UserDataFolderPath,
            SettingsExportFolder = AppConstants.SettingsDirectoryPath,
            WindowSizePreset = AppConstants.DefaultWindowSizePreset,
            Window = new WindowSettings()
        };
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        settings.HomeUrl = NormalizeUrl(settings.HomeUrl, AppConstants.DefaultHomeUrl);
        settings.LastUrl = NormalizeUrl(settings.LastUrl, settings.HomeUrl);
        settings.PlayerModeReferer = NormalizeReferer(settings.PlayerModeReferer);
        settings.WebView2UserDataFolder = NormalizePath(
            settings.WebView2UserDataFolder,
            AppConstants.WebView2UserDataFolderPath);
        settings.SettingsExportFolder = NormalizePath(
            settings.SettingsExportFolder,
            AppConstants.SettingsDirectoryPath);
        settings.WindowSizePreset = NormalizeWindowSizePreset(settings.WindowSizePreset);
        settings.Window ??= new WindowSettings();

        settings.Window.Width = Clamp(settings.Window.Width, AppConstants.MinWindowWidth, AppConstants.MaxWindowWidth);
        settings.Window.Height = Clamp(settings.Window.Height, AppConstants.MinWindowHeight, AppConstants.MaxWindowHeight);

        return settings;
    }

    private static string NormalizeUrl(string? value, string fallback)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out _) ? value! : fallback;
    }

    private static string NormalizeReferer(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value)
            ? AppConstants.DefaultPlayerModeRefererValue
            : value.Trim();

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && !string.IsNullOrWhiteSpace(uri.Host)
                ? uri.ToString()
                : AppConstants.DefaultPlayerModeRefererValue;
    }

    private static string NormalizePath(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeWindowSizePreset(string? value)
    {
        var preset = string.IsNullOrWhiteSpace(value) ? AppConstants.DefaultWindowSizePreset : value.Trim();
        if (preset == AppConstants.CustomWindowSizePresetLabel)
        {
            return AppConstants.CustomWindowSizePreset;
        }

        return AppConstants.TryGetWindowSizePreset(preset, out _, out _) || preset == AppConstants.CustomWindowSizePreset
            ? preset
            : AppConstants.CustomWindowSizePreset;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
