using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using Forms = System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
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
    private readonly WpfComboBox[] _favoriteIconShapeComboBoxes;
    private readonly WpfCheckBox[] _favoriteIconRoundedCheckBoxes;
    private readonly WpfCheckBox[] _favoriteBoldCheckBoxes;
    private readonly WpfComboBox[] _favoritePlaybackModeComboBoxes;
    private readonly WpfCheckBox[] _favoriteAutoplayCheckBoxes;
    private readonly WpfCheckBox[] _favoriteMuteCheckBoxes;
    private readonly WpfCheckBox[] _favoriteLoopCheckBoxes;
    private readonly WpfCheckBox[] _favoriteResumeCheckBoxes;
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
        _favoriteIconShapeComboBoxes =
        [
            FavoriteIconShapeComboBox01, FavoriteIconShapeComboBox02, FavoriteIconShapeComboBox03, FavoriteIconShapeComboBox04, FavoriteIconShapeComboBox05,
            FavoriteIconShapeComboBox06, FavoriteIconShapeComboBox07, FavoriteIconShapeComboBox08, FavoriteIconShapeComboBox09, FavoriteIconShapeComboBox10
        ];
        _favoriteIconRoundedCheckBoxes =
        [
            FavoriteIconRoundedCheckBox01, FavoriteIconRoundedCheckBox02, FavoriteIconRoundedCheckBox03, FavoriteIconRoundedCheckBox04, FavoriteIconRoundedCheckBox05,
            FavoriteIconRoundedCheckBox06, FavoriteIconRoundedCheckBox07, FavoriteIconRoundedCheckBox08, FavoriteIconRoundedCheckBox09, FavoriteIconRoundedCheckBox10
        ];
        _favoriteBoldCheckBoxes =
        [
            FavoriteBoldCheckBox01, FavoriteBoldCheckBox02, FavoriteBoldCheckBox03, FavoriteBoldCheckBox04, FavoriteBoldCheckBox05,
            FavoriteBoldCheckBox06, FavoriteBoldCheckBox07, FavoriteBoldCheckBox08, FavoriteBoldCheckBox09, FavoriteBoldCheckBox10
        ];
        _favoritePlaybackModeComboBoxes =
        [
            FavoritePlaybackModeComboBox01, FavoritePlaybackModeComboBox02, FavoritePlaybackModeComboBox03, FavoritePlaybackModeComboBox04, FavoritePlaybackModeComboBox05,
            FavoritePlaybackModeComboBox06, FavoritePlaybackModeComboBox07, FavoritePlaybackModeComboBox08, FavoritePlaybackModeComboBox09, FavoritePlaybackModeComboBox10
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
        _favoriteResumeCheckBoxes =
        [
            FavoriteResumeCheckBox01, FavoriteResumeCheckBox02, FavoriteResumeCheckBox03, FavoriteResumeCheckBox04, FavoriteResumeCheckBox05,
            FavoriteResumeCheckBox06, FavoriteResumeCheckBox07, FavoriteResumeCheckBox08, FavoriteResumeCheckBox09, FavoriteResumeCheckBox10
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
        return Enumerable.Range(0, AppConstants.MaxBookmarks)
            .Select(index => new BookmarkItem
            {
                Label = _favoriteLabelTextBoxes[index].Text.Trim(),
                Url = _favoriteUrlTextBoxes[index].Text.Trim(),
                SortOrder = index + 1,
                IsEnabled = _favoriteEnabledCheckBoxes[index].IsChecked == true,
                IconPath = _favoriteIconPathTextBoxes[index].Text.Trim(),
                IconShape = GetSelectedIconShape(_favoriteIconShapeComboBoxes[index]),
                IconRounded = _favoriteIconRoundedCheckBoxes[index].IsChecked == true,
                IsBold = _favoriteBoldCheckBoxes[index].IsChecked == true,
                PlaybackMode = GetSelectedPlaybackMode(_favoritePlaybackModeComboBoxes[index]),
                Autoplay = _favoriteAutoplayCheckBoxes[index].IsChecked == true,
                Mute = _favoriteMuteCheckBoxes[index].IsChecked == true,
                Loop = _favoriteLoopCheckBoxes[index].IsChecked == true,
                ResumePlayback = _favoriteResumeCheckBoxes[index].IsChecked == true,
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
            _favoritePlaybackModeComboBoxes[index].SelectionChanged += (_, _) => UpdatePlaybackOptionState(colorIndex);
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
        SelectComboBoxItem(_favoriteIconShapeComboBoxes[index], GetIconShapeLabel(bookmark.IconShape));
        _favoriteIconRoundedCheckBoxes[index].IsChecked = bookmark.IconRounded;
        _favoriteBoldCheckBoxes[index].IsChecked = bookmark.IsBold;
        SelectComboBoxItem(_favoritePlaybackModeComboBoxes[index], GetPlaybackModeLabel(bookmark.PlaybackMode));
        _favoriteAutoplayCheckBoxes[index].IsChecked = bookmark.Autoplay;
        _favoriteMuteCheckBoxes[index].IsChecked = bookmark.Mute;
        _favoriteLoopCheckBoxes[index].IsChecked = bookmark.Loop;
        _favoriteResumeCheckBoxes[index].IsChecked = bookmark.ResumePlayback;
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
        var isPlayerMode = GetSelectedPlaybackMode(_favoritePlaybackModeComboBoxes[index]) == AppConstants.PlayerPlaybackMode;
        _favoriteAutoplayCheckBoxes[index].IsEnabled = isPlayerMode;
        _favoriteMuteCheckBoxes[index].IsEnabled = isPlayerMode;
        _favoriteLoopCheckBoxes[index].IsEnabled = isPlayerMode;
        _favoriteResumeCheckBoxes[index].IsEnabled = true;
    }

    private static void SelectComboBoxItem(WpfComboBox comboBox, string text)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content?.ToString() == text)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetSelectedComboBoxText(WpfComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
    }

    private static string GetIconShapeLabel(string value)
    {
        return value == AppConstants.RectangleBookmarkIconShape
            ? AppConstants.BookmarkIconShapeRectangleLabel
            : AppConstants.BookmarkIconShapeSquareLabel;
    }

    private static string GetSelectedIconShape(WpfComboBox comboBox)
    {
        return GetSelectedComboBoxText(comboBox, AppConstants.BookmarkIconShapeSquareLabel) == AppConstants.BookmarkIconShapeRectangleLabel
            ? AppConstants.RectangleBookmarkIconShape
            : AppConstants.DefaultBookmarkIconShape;
    }

    private static string GetPlaybackModeLabel(string value)
    {
        return value == AppConstants.PlayerPlaybackMode
            ? AppConstants.PlaybackModePlayerLabel
            : AppConstants.PlaybackModeNormalLabel;
    }

    private static string GetSelectedPlaybackMode(WpfComboBox comboBox)
    {
        return GetSelectedComboBoxText(comboBox, AppConstants.PlaybackModeNormalLabel) == AppConstants.PlaybackModePlayerLabel
            ? AppConstants.PlayerPlaybackMode
            : AppConstants.DefaultPlaybackMode;
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
            Title = "アイコン画像を選択",
            InitialDirectory = AppPathService.GetIconsDirectoryPath(),
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|すべてのファイル (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        iconPathTextBox.Text = ToProjectRelativePath(dialog.FileName);
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
