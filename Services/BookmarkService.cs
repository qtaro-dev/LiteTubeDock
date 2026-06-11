using System.IO;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public sealed class BookmarkService : JsonFileService
{
    private readonly string _bookmarksFilePath;

    public BookmarkService()
        : this(AppConstants.BookmarksFilePath)
    {
    }

    public BookmarkService(string bookmarksFilePath)
    {
        _bookmarksFilePath = bookmarksFilePath;
    }

    public string BookmarksFilePath => _bookmarksFilePath;

    public IReadOnlyList<BookmarkItem> Load()
    {
        Directory.CreateDirectory(AppConstants.SettingsDirectoryPath);
        EnsureDirectory(_bookmarksFilePath);
        Directory.CreateDirectory(AppPathService.GetIconsDirectoryPath());

        if (!File.Exists(_bookmarksFilePath))
        {
            var bookmarks = CreateDefault();
            Save(bookmarks);
            return bookmarks;
        }

        try
        {
            var json = File.ReadAllText(_bookmarksFilePath);
            var bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(json, JsonOptions);
            return Normalize(bookmarks);
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

    public bool TryLoadExisting(out IReadOnlyList<BookmarkItem> bookmarks)
    {
        bookmarks = Array.Empty<BookmarkItem>();
        Directory.CreateDirectory(AppConstants.SettingsDirectoryPath);
        EnsureDirectory(_bookmarksFilePath);
        Directory.CreateDirectory(AppPathService.GetIconsDirectoryPath());

        if (!File.Exists(_bookmarksFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(_bookmarksFilePath);
            var loaded = JsonSerializer.Deserialize<List<BookmarkItem>>(json, JsonOptions);
            bookmarks = Normalize(loaded);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Save(IEnumerable<BookmarkItem> bookmarks)
    {
        Directory.CreateDirectory(AppConstants.SettingsDirectoryPath);
        EnsureDirectory(_bookmarksFilePath);
        Directory.CreateDirectory(AppPathService.GetIconsDirectoryPath());
        var normalized = Normalize(bookmarks);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(_bookmarksFilePath, json);
    }

    public void ExportToFile(string exportFilePath)
    {
        EnsureDirectory(exportFilePath);
        var bookmarks = Load();
        var json = JsonSerializer.Serialize(bookmarks, JsonOptions);
        File.WriteAllText(exportFilePath, json);
    }

    public IReadOnlyList<BookmarkItem> ImportFromFile(string importFilePath)
    {
        var json = File.ReadAllText(importFilePath);
        var bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(json, JsonOptions);
        var normalized = Normalize(bookmarks);
        Save(normalized);
        return normalized;
    }

    public IReadOnlyList<BookmarkItem> CreateDefault()
    {
        return Enumerable.Range(1, AppConstants.MaxBookmarks)
            .Select(index => new BookmarkItem
            {
                Label = index == 1
                    ? AppConstants.DefaultHomeBookmarkLabel
                    : $"{AppConstants.DefaultBookmarkLabelPrefix}{index}",
                Url = AppConstants.DefaultHomeUrl,
                SortOrder = index,
                IsEnabled = index == 1,
                BackgroundColor = AppConstants.DefaultBookmarkBackgroundColor,
                ForegroundColor = AppConstants.DefaultBookmarkForegroundColor,
                IsBold = false,
                IconPath = string.Empty,
                IconShape = AppConstants.DefaultBookmarkIconShape,
                IconRounded = true,
                PlaybackMode = AppConstants.DefaultPlaybackMode,
                Autoplay = false,
                Mute = false,
                Loop = false,
                ResumePlayback = true
            })
            .ToList();
    }

    public static BookmarkItem CreateEmptySlot(int sortOrder)
    {
        return new BookmarkItem
        {
            Label = $"{AppConstants.DefaultBookmarkLabelPrefix}{sortOrder}",
            Url = AppConstants.DefaultHomeUrl,
            SortOrder = sortOrder,
            IsEnabled = false,
            BackgroundColor = AppConstants.DefaultBookmarkBackgroundColor,
            ForegroundColor = AppConstants.DefaultBookmarkForegroundColor,
            IsBold = false,
            IconPath = string.Empty,
            IconShape = AppConstants.DefaultBookmarkIconShape,
            IconRounded = true,
            PlaybackMode = AppConstants.DefaultPlaybackMode,
            Autoplay = false,
            Mute = false,
            Loop = false,
            ResumePlayback = true
        };
    }

    private static IReadOnlyList<BookmarkItem> Normalize(IEnumerable<BookmarkItem>? bookmarks)
    {
        var ordered = (bookmarks ?? Enumerable.Empty<BookmarkItem>())
            .Where(item => item is not null)
            .OrderBy(item => item.SortOrder)
            .Take(AppConstants.MaxBookmarks)
            .Select((item, index) => new BookmarkItem
            {
                Label = string.IsNullOrWhiteSpace(item.Label) ? AppConstants.EmptyBookmarkLabel : item.Label.Trim(),
                Url = item.Url?.Trim() ?? string.Empty,
                SortOrder = index + 1,
                IsEnabled = item.IsEnabled && Uri.TryCreate(item.Url, UriKind.Absolute, out _),
                BackgroundColor = NormalizeColor(item.BackgroundColor, AppConstants.DefaultBookmarkBackgroundColor),
                ForegroundColor = NormalizeColor(item.ForegroundColor, AppConstants.DefaultBookmarkForegroundColor),
                IsBold = item.IsBold,
                IconPath = NormalizeIconPath(item.IconPath),
                IconShape = NormalizeIconShape(item.IconShape),
                IconRounded = item.IconRounded,
                PlaybackMode = NormalizePlaybackMode(item.PlaybackMode),
                Autoplay = item.Autoplay,
                Mute = item.Mute,
                Loop = item.Loop,
                ResumePlayback = item.ResumePlayback
            })
            .ToList();

        while (ordered.Count < AppConstants.MaxBookmarks)
        {
            ordered.Add(new BookmarkItem
            {
                Label = AppConstants.EmptyBookmarkLabel,
                Url = string.Empty,
                SortOrder = ordered.Count + 1,
                IsEnabled = false,
                BackgroundColor = AppConstants.DefaultBookmarkBackgroundColor,
                ForegroundColor = AppConstants.DefaultBookmarkForegroundColor,
                IsBold = false,
                IconPath = string.Empty,
                IconShape = AppConstants.DefaultBookmarkIconShape,
                IconRounded = true,
                PlaybackMode = AppConstants.DefaultPlaybackMode,
                Autoplay = false,
                Mute = false,
                Loop = false,
                ResumePlayback = true
            });
        }

        return ordered;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return IsHexRgbColor(trimmed) ? trimmed.ToUpperInvariant() : fallback;
    }

    private static bool IsHexRgbColor(string value)
    {
        return value.Length == 7
            && value[0] == '#'
            && value.Skip(1).All(Uri.IsHexDigit);
    }

    private static string NormalizeIconPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var extension = Path.GetExtension(trimmed).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp"
            ? trimmed
            : string.Empty;
    }

    private static string NormalizeIconShape(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? AppConstants.DefaultBookmarkIconShape
            : value.Trim();

        return trimmed is AppConstants.DefaultBookmarkIconShape or AppConstants.RectangleBookmarkIconShape
            ? trimmed
            : AppConstants.DefaultBookmarkIconShape;
    }

    private static string NormalizePlaybackMode(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value)
            ? AppConstants.DefaultPlaybackMode
            : value.Trim();

        return trimmed is AppConstants.DefaultPlaybackMode or AppConstants.PlayerPlaybackMode
            ? trimmed
            : AppConstants.DefaultPlaybackMode;
    }
}
