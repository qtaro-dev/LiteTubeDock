using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using Forms = System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LiteTubeDock.Views;

public partial class FavoriteButtonsSettingsView : System.Windows.Controls.UserControl
{
    private readonly WpfTextBox[] _favoriteLabelTextBoxes;
    private readonly WpfTextBox[] _favoriteUrlTextBoxes;
    private readonly WpfTextBox[] _favoriteIconPathTextBoxes;
    private readonly WpfTextBox[] _favoriteBackgroundColorTextBoxes;
    private readonly WpfTextBox[] _favoriteForegroundColorTextBoxes;
    private readonly WpfButton[] _favoriteBackgroundColorButtons;
    private readonly WpfButton[] _favoriteForegroundColorButtons;
    private readonly Border[] _favoriteSlotNumberBands;
    private readonly WpfTextBlock[] _favoriteSlotNumberTexts;
    private readonly WpfButton[] _favoriteIconSelectButtons;
    private readonly WpfCheckBox[] _favoriteBoldCheckBoxes;
    private readonly WpfTextBox[] _favoriteStartPositionTextBoxes;
    private readonly WpfCheckBox[] _favoriteAutoplayCheckBoxes;
    private readonly WpfCheckBox[] _favoriteMuteCheckBoxes;
    private readonly WpfCheckBox[] _favoriteLoopCheckBoxes;
    private readonly WpfCheckBox[] _favoriteEnabledCheckBoxes;
    private readonly WpfButton[] _favoriteDeleteButtons;

    public FavoriteButtonsSettingsView()
    {
        InitializeComponent();

        _favoriteLabelTextBoxes =
        [
            FavoriteLabelTextBox01, FavoriteLabelTextBox02, FavoriteLabelTextBox03, FavoriteLabelTextBox04, FavoriteLabelTextBox05,
            FavoriteLabelTextBox06, FavoriteLabelTextBox07, FavoriteLabelTextBox08, FavoriteLabelTextBox09, FavoriteLabelTextBox10
        ];
        _favoriteUrlTextBoxes =
        [
            FavoriteUrlTextBox01, FavoriteUrlTextBox02, FavoriteUrlTextBox03, FavoriteUrlTextBox04, FavoriteUrlTextBox05,
            FavoriteUrlTextBox06, FavoriteUrlTextBox07, FavoriteUrlTextBox08, FavoriteUrlTextBox09, FavoriteUrlTextBox10
        ];
        _favoriteIconPathTextBoxes =
        [
            FavoriteIconPathTextBox01, FavoriteIconPathTextBox02, FavoriteIconPathTextBox03, FavoriteIconPathTextBox04, FavoriteIconPathTextBox05,
            FavoriteIconPathTextBox06, FavoriteIconPathTextBox07, FavoriteIconPathTextBox08, FavoriteIconPathTextBox09, FavoriteIconPathTextBox10
        ];
        _favoriteBackgroundColorTextBoxes =
        [
            FavoriteBackgroundColorTextBox01, FavoriteBackgroundColorTextBox02, FavoriteBackgroundColorTextBox03, FavoriteBackgroundColorTextBox04, FavoriteBackgroundColorTextBox05,
            FavoriteBackgroundColorTextBox06, FavoriteBackgroundColorTextBox07, FavoriteBackgroundColorTextBox08, FavoriteBackgroundColorTextBox09, FavoriteBackgroundColorTextBox10
        ];
        _favoriteForegroundColorTextBoxes =
        [
            FavoriteForegroundColorTextBox01, FavoriteForegroundColorTextBox02, FavoriteForegroundColorTextBox03, FavoriteForegroundColorTextBox04, FavoriteForegroundColorTextBox05,
            FavoriteForegroundColorTextBox06, FavoriteForegroundColorTextBox07, FavoriteForegroundColorTextBox08, FavoriteForegroundColorTextBox09, FavoriteForegroundColorTextBox10
        ];
        _favoriteBackgroundColorButtons =
        [
            FavoriteBackgroundColorButton01, FavoriteBackgroundColorButton02, FavoriteBackgroundColorButton03, FavoriteBackgroundColorButton04, FavoriteBackgroundColorButton05,
            FavoriteBackgroundColorButton06, FavoriteBackgroundColorButton07, FavoriteBackgroundColorButton08, FavoriteBackgroundColorButton09, FavoriteBackgroundColorButton10
        ];
        _favoriteForegroundColorButtons =
        [
            FavoriteForegroundColorButton01, FavoriteForegroundColorButton02, FavoriteForegroundColorButton03, FavoriteForegroundColorButton04, FavoriteForegroundColorButton05,
            FavoriteForegroundColorButton06, FavoriteForegroundColorButton07, FavoriteForegroundColorButton08, FavoriteForegroundColorButton09, FavoriteForegroundColorButton10
        ];
        _favoriteSlotNumberBands =
        [
            FavoriteSlotNumberBand01, FavoriteSlotNumberBand02, FavoriteSlotNumberBand03, FavoriteSlotNumberBand04, FavoriteSlotNumberBand05,
            FavoriteSlotNumberBand06, FavoriteSlotNumberBand07, FavoriteSlotNumberBand08, FavoriteSlotNumberBand09, FavoriteSlotNumberBand10
        ];
        _favoriteSlotNumberTexts =
        [
            FavoriteSlotNumberText01, FavoriteSlotNumberText02, FavoriteSlotNumberText03, FavoriteSlotNumberText04, FavoriteSlotNumberText05,
            FavoriteSlotNumberText06, FavoriteSlotNumberText07, FavoriteSlotNumberText08, FavoriteSlotNumberText09, FavoriteSlotNumberText10
        ];
        _favoriteIconSelectButtons =
        [
            FavoriteIconSelectButton01, FavoriteIconSelectButton02, FavoriteIconSelectButton03, FavoriteIconSelectButton04, FavoriteIconSelectButton05,
            FavoriteIconSelectButton06, FavoriteIconSelectButton07, FavoriteIconSelectButton08, FavoriteIconSelectButton09, FavoriteIconSelectButton10
        ];
        _favoriteBoldCheckBoxes =
        [
            FavoriteBoldCheckBox01, FavoriteBoldCheckBox02, FavoriteBoldCheckBox03, FavoriteBoldCheckBox04, FavoriteBoldCheckBox05,
            FavoriteBoldCheckBox06, FavoriteBoldCheckBox07, FavoriteBoldCheckBox08, FavoriteBoldCheckBox09, FavoriteBoldCheckBox10
        ];
        _favoriteStartPositionTextBoxes =
        [
            FavoriteStartPositionTextBox01, FavoriteStartPositionTextBox02, FavoriteStartPositionTextBox03, FavoriteStartPositionTextBox04, FavoriteStartPositionTextBox05,
            FavoriteStartPositionTextBox06, FavoriteStartPositionTextBox07, FavoriteStartPositionTextBox08, FavoriteStartPositionTextBox09, FavoriteStartPositionTextBox10
        ];
        _favoriteAutoplayCheckBoxes =
        [
            FavoriteAutoplayCheckBox01, FavoriteAutoplayCheckBox02, FavoriteAutoplayCheckBox03, FavoriteAutoplayCheckBox04, FavoriteAutoplayCheckBox05,
            FavoriteAutoplayCheckBox06, FavoriteAutoplayCheckBox07, FavoriteAutoplayCheckBox08, FavoriteAutoplayCheckBox09, FavoriteAutoplayCheckBox10
        ];
        _favoriteMuteCheckBoxes =
        [
            FavoriteMuteCheckBox01, FavoriteMuteCheckBox02, FavoriteMuteCheckBox03, FavoriteMuteCheckBox04, FavoriteMuteCheckBox05,
            FavoriteMuteCheckBox06, FavoriteMuteCheckBox07, FavoriteMuteCheckBox08, FavoriteMuteCheckBox09, FavoriteMuteCheckBox10
        ];
        _favoriteLoopCheckBoxes =
        [
            FavoriteLoopCheckBox01, FavoriteLoopCheckBox02, FavoriteLoopCheckBox03, FavoriteLoopCheckBox04, FavoriteLoopCheckBox05,
            FavoriteLoopCheckBox06, FavoriteLoopCheckBox07, FavoriteLoopCheckBox08, FavoriteLoopCheckBox09, FavoriteLoopCheckBox10
        ];
        _favoriteEnabledCheckBoxes =
        [
            FavoriteEnabledCheckBox01, FavoriteEnabledCheckBox02, FavoriteEnabledCheckBox03, FavoriteEnabledCheckBox04, FavoriteEnabledCheckBox05,
            FavoriteEnabledCheckBox06, FavoriteEnabledCheckBox07, FavoriteEnabledCheckBox08, FavoriteEnabledCheckBox09, FavoriteEnabledCheckBox10
        ];
        _favoriteDeleteButtons =
        [
            FavoriteDeleteButton01, FavoriteDeleteButton02, FavoriteDeleteButton03, FavoriteDeleteButton04, FavoriteDeleteButton05,
            FavoriteDeleteButton06, FavoriteDeleteButton07, FavoriteDeleteButton08, FavoriteDeleteButton09, FavoriteDeleteButton10
        ];

        AttachEvents();
    }

    public void LoadBookmarks(IReadOnlyList<BookmarkItem> bookmarks)
    {
        for (var index = 0; index < AppConstants.MaxBookmarks; index++)
        {
            var bookmark = bookmarks.ElementAtOrDefault(index) ?? new BookmarkItem { SortOrder = index + 1 };
            ApplyBookmarkToForm(index, bookmark);
        }
    }

    public IReadOnlyList<BookmarkItem> CollectBookmarks()
    {
        var startPositions = new int[AppConstants.MaxBookmarks];
        for (var index = 0; index < AppConstants.MaxBookmarks; index++)
        {
            if (!TryNormalizeStartPositionText(_favoriteStartPositionTextBoxes[index].Text, out var seconds, out var normalized))
            {
                var slotNumber = index + 1;
                System.Windows.MessageBox.Show(
                    $"お気に入り{slotNumber:00}のスタート位置が不正です。HH:mm:ss または数字のみで入力してください。",
                    AppConstants.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _favoriteStartPositionTextBoxes[index].Focus();
                throw new InvalidOperationException($"Favorite start position is invalid. Slot={slotNumber}");
            }

            _favoriteStartPositionTextBoxes[index].Text = normalized;
            startPositions[index] = seconds;
        }

        return Enumerable.Range(0, AppConstants.MaxBookmarks)
            .Select(index => new BookmarkItem
            {
                Label = _favoriteLabelTextBoxes[index].Text.Trim(),
                Url = _favoriteUrlTextBoxes[index].Text.Trim(),
                SortOrder = index + 1,
                IsEnabled = _favoriteEnabledCheckBoxes[index].IsChecked == true,
                IconPath = _favoriteIconPathTextBoxes[index].Text.Trim(),
                IsBold = _favoriteBoldCheckBoxes[index].IsChecked == true,
                Autoplay = _favoriteAutoplayCheckBoxes[index].IsChecked == true,
                Mute = _favoriteMuteCheckBoxes[index].IsChecked == true,
                Loop = _favoriteLoopCheckBoxes[index].IsChecked == true,
                StartPositionSeconds = startPositions[index],
                BackgroundColor = _favoriteBackgroundColorTextBoxes[index].Text.Trim(),
                ForegroundColor = _favoriteForegroundColorTextBoxes[index].Text.Trim()
            })
            .ToList();
    }

    private void AttachEvents()
    {
        for (var index = 0; index < AppConstants.MaxBookmarks; index++)
        {
            var colorIndex = index;
            _favoriteBackgroundColorButtons[index].Click += (_, _) => SelectColor(
                _favoriteBackgroundColorTextBoxes[colorIndex],
                _favoriteBackgroundColorButtons[colorIndex],
                AppConstants.DefaultBookmarkBackgroundColor);
            _favoriteForegroundColorButtons[index].Click += (_, _) => SelectColor(
                _favoriteForegroundColorTextBoxes[colorIndex],
                _favoriteForegroundColorButtons[colorIndex],
                AppConstants.DefaultBookmarkForegroundColor);
            _favoriteBackgroundColorTextBoxes[index].TextChanged += (_, _) => UpdateColorPreview(
                _favoriteBackgroundColorTextBoxes[colorIndex],
                _favoriteBackgroundColorButtons[colorIndex],
                AppConstants.DefaultBookmarkBackgroundColor);
            _favoriteForegroundColorTextBoxes[index].TextChanged += (_, _) => UpdateColorPreview(
                _favoriteForegroundColorTextBoxes[colorIndex],
                _favoriteForegroundColorButtons[colorIndex],
                AppConstants.DefaultBookmarkForegroundColor);
            _favoriteBackgroundColorTextBoxes[index].TextChanged += (_, _) => UpdateSlotNumberBandPreview(colorIndex);
            _favoriteForegroundColorTextBoxes[index].TextChanged += (_, _) => UpdateSlotNumberBandPreview(colorIndex);
            _favoriteIconSelectButtons[index].Click += (_, _) => SelectIconPath(_favoriteIconPathTextBoxes[colorIndex]);
            _favoriteStartPositionTextBoxes[index].LostKeyboardFocus += (_, _) => NormalizeStartPositionTextBox(colorIndex);
            _favoriteStartPositionTextBoxes[index].KeyDown += (_, e) => NormalizeStartPositionTextBoxOnEnter(colorIndex, e);
            _favoriteDeleteButtons[index].Click += (_, _) => DeleteFavoriteSlot(colorIndex);
        }
    }

    public void RefreshPlaybackOptionStates()
    {
        for (var index = 0; index < AppConstants.MaxBookmarks; index++)
        {
            UpdatePlaybackOptionState(index);
        }
    }

    private void DeleteFavoriteSlot(int index)
    {
        var result = System.Windows.MessageBox.Show(
            AppConstants.FavoriteDeleteConfirmMessage,
            AppConstants.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        ApplyBookmarkToForm(index, BookmarkService.CreateEmptySlot(index + 1));
    }

    private void ApplyBookmarkToForm(int index, BookmarkItem bookmark)
    {
        _favoriteLabelTextBoxes[index].Text = bookmark.Label;
        _favoriteUrlTextBoxes[index].Text = bookmark.Url;
        _favoriteIconPathTextBoxes[index].Text = bookmark.IconPath;
        _favoriteBoldCheckBoxes[index].IsChecked = bookmark.IsBold;
        _favoriteStartPositionTextBoxes[index].Text = FormatStartPosition(bookmark.StartPositionSeconds);
        _favoriteAutoplayCheckBoxes[index].IsChecked = bookmark.Autoplay;
        _favoriteMuteCheckBoxes[index].IsChecked = bookmark.Mute;
        _favoriteLoopCheckBoxes[index].IsChecked = bookmark.Loop;
        _favoriteBackgroundColorTextBoxes[index].Text = bookmark.BackgroundColor;
        _favoriteForegroundColorTextBoxes[index].Text = bookmark.ForegroundColor;
        UpdateColorPreview(_favoriteBackgroundColorTextBoxes[index], _favoriteBackgroundColorButtons[index], AppConstants.DefaultBookmarkBackgroundColor);
        UpdateColorPreview(_favoriteForegroundColorTextBoxes[index], _favoriteForegroundColorButtons[index], AppConstants.DefaultBookmarkForegroundColor);
        UpdateSlotNumberBandPreview(index);
        _favoriteEnabledCheckBoxes[index].IsChecked = bookmark.IsEnabled;
        UpdatePlaybackOptionState(index);
    }

    private void UpdateSlotNumberBandPreview(int index)
    {
        var backgroundColor = NormalizeColor(_favoriteBackgroundColorTextBoxes[index].Text, AppConstants.DefaultBookmarkBackgroundColor);
        var foregroundColor = NormalizeColor(_favoriteForegroundColorTextBoxes[index].Text, AppConstants.DefaultBookmarkForegroundColor);

        _favoriteSlotNumberBands[index].Background = new SolidColorBrush(backgroundColor);
        _favoriteSlotNumberTexts[index].Foreground = new SolidColorBrush(foregroundColor);
    }

    private void UpdatePlaybackOptionState(int index)
    {
        _favoriteAutoplayCheckBoxes[index].IsEnabled = true;
        _favoriteMuteCheckBoxes[index].IsEnabled = true;
        _favoriteLoopCheckBoxes[index].IsEnabled = true;
    }

    private void NormalizeStartPositionTextBoxOnEnter(int index, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        NormalizeStartPositionTextBox(index);
        e.Handled = true;
    }

    private void NormalizeStartPositionTextBox(int index)
    {
        if (TryNormalizeStartPositionText(_favoriteStartPositionTextBoxes[index].Text, out _, out var normalized))
        {
            _favoriteStartPositionTextBoxes[index].Text = normalized;
        }
    }

    private static bool TryNormalizeStartPositionText(string? value, out int seconds, out string normalized)
    {
        seconds = 0;
        normalized = "00:00:00";
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.All(char.IsDigit))
        {
            return TryNormalizeDigitsOnlyStartPosition(trimmed, out seconds, out normalized);
        }

        if (trimmed.Any(c => !char.IsDigit(c) && c != ':'))
        {
            return false;
        }

        var parts = trimmed.Split(':');
        if (parts.Length != 3
            || parts.Any(part => part.Length == 0 || !part.All(char.IsDigit))
            || !int.TryParse(parts[0], out var hours)
            || !int.TryParse(parts[1], out var minutes)
            || !int.TryParse(parts[2], out var startSeconds)
            || minutes is < 0 or > 59
            || startSeconds is < 0 or > 59
            || hours < 0)
        {
            return false;
        }

        seconds = hours * 3600 + minutes * 60 + startSeconds;
        normalized = FormatStartPosition(seconds);
        return true;
    }

    private static bool TryNormalizeDigitsOnlyStartPosition(string value, out int seconds, out string normalized)
    {
        seconds = 0;
        normalized = "00:00:00";
        var padded = value.PadLeft(6, '0');
        var secondsPart = padded[^2..];
        var minutesPart = padded[^4..^2];
        var hoursPart = padded[..^4];

        if (!int.TryParse(hoursPart, out var hours)
            || !int.TryParse(minutesPart, out var minutes)
            || !int.TryParse(secondsPart, out var startSeconds)
            || minutes is > 59
            || startSeconds is > 59)
        {
            return false;
        }

        seconds = hours * 3600 + minutes * 60 + startSeconds;
        normalized = FormatStartPosition(seconds);
        return true;
    }

    private static string FormatStartPosition(int totalSeconds)
    {
        var safeSeconds = Math.Max(0, totalSeconds);
        var hours = safeSeconds / 3600;
        var minutes = safeSeconds % 3600 / 60;
        var seconds = safeSeconds % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private static void SelectColor(WpfTextBox colorTextBox, WpfButton previewButton, string fallback)
    {
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = ToDrawingColor(colorTextBox.Text, fallback)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        var colorCode = ToHexColor(dialog.Color);
        colorTextBox.Text = colorCode;
        UpdateColorPreview(colorTextBox, previewButton, fallback);
    }

    private static void SelectIconPath(WpfTextBox iconPathTextBox)
    {
        Directory.CreateDirectory(AppPathService.GetIconsDirectoryPath());

        using var dialog = new Forms.OpenFileDialog
        {
            Title = "背景画像を選択",
            InitialDirectory = AppPathService.GetIconsDirectoryPath(),
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|すべてのファイル (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        if (!TryValidateFavoriteBackgroundImage(dialog.FileName))
        {
            return;
        }

        iconPathTextBox.Text = ToProjectRelativePath(dialog.FileName);
    }

    private static bool TryValidateFavoriteBackgroundImage(string path)
    {
        if (!File.Exists(path))
        {
            ShowBackgroundImageError(AppConstants.FavoriteBackgroundImageLoadFailedMessage);
            return false;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
        {
            ShowBackgroundImageError(AppConstants.FavoriteBackgroundImageUnsupportedFormatMessage);
            return false;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > AppConstants.FavoriteBackgroundImageMaxBytes)
        {
            ShowBackgroundImageError(
                AppConstants.FavoriteBackgroundImageFileTooLargeMessage
                + Environment.NewLine
                + $"実ファイルサイズ: {FormatFileSize(fileInfo.Length)}");
            return false;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                ShowBackgroundImageError(AppConstants.FavoriteBackgroundImageLoadFailedMessage);
                return false;
            }

            if (frame.PixelWidth > AppConstants.FavoriteBackgroundImageMaxWidth
                || frame.PixelHeight > AppConstants.FavoriteBackgroundImageMaxHeight)
            {
                ShowBackgroundImageError(
                    AppConstants.FavoriteBackgroundImageTooLargeMessage
                    + Environment.NewLine
                    + $"実サイズ: {frame.PixelWidth} x {frame.PixelHeight}px");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            ShowBackgroundImageError(AppConstants.FavoriteBackgroundImageLoadFailedMessage);
            return false;
        }
    }

    private static void ShowBackgroundImageError(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            AppConstants.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static string FormatFileSize(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return $"{megabytes:0.##}MB ({bytes} bytes)";
    }

    private static string ToProjectRelativePath(string path)
    {
        var root = Path.GetFullPath(AppPathService.GetProjectRootPath());
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(root, fullPath);
        var isOutsideProject = relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        return isOutsideProject ? fullPath : relativePath;
    }

    private static void UpdateColorPreview(WpfTextBox colorTextBox, WpfButton previewButton, string fallback)
    {
        var color = NormalizeColor(colorTextBox.Text, fallback);
        previewButton.Background = new SolidColorBrush(color);
        previewButton.ToolTip = colorTextBox.Text;
    }

    private static WpfColor NormalizeColor(string? value, string fallback)
    {
        return TryParseHexColor(value, out var color)
            ? color
            : TryParseHexColor(fallback, out var fallbackColor)
                ? fallbackColor
                : Colors.Transparent;
    }

    private static DrawingColor ToDrawingColor(string? value, string fallback)
    {
        var color = NormalizeColor(value, fallback);
        return DrawingColor.FromArgb(color.R, color.G, color.B);
    }

    private static string ToHexColor(DrawingColor color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseHexColor(string? value, out WpfColor color)
    {
        color = Colors.Transparent;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#' || !trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            return false;
        }

        try
        {
            color = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(trimmed);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
