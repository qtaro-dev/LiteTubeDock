using System.Windows;
using System.Windows.Input;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using LiteTubeDock.Views;

namespace LiteTubeDock;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly BookmarkService _bookmarkService;
    private readonly GeneralSettingsView _generalSettingsView;
    private readonly FavoriteButtonsSettingsView _favoriteButtonsSettingsView;

    private AppSettings _settings;
    private IReadOnlyList<BookmarkItem> _bookmarks;
    private System.Windows.Point _middleScrollStartPoint;
    private double _middleScrollStartOffset;
    private bool _isMiddleButtonScrolling;
    private System.Windows.Input.Cursor? _previousCursor;

    public bool ResetWindowBoundsRequested => _generalSettingsView.ResetWindowBoundsRequested;

    public bool WindowSizeChanged => _generalSettingsView.WindowSizeChanged;

    public SettingsWindow()
        : this(new SettingsService(), new BookmarkService())
    {
    }

    public SettingsWindow(SettingsService settingsService, BookmarkService bookmarkService)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _bookmarkService = bookmarkService;
        _settings = _settingsService.Load();
        _bookmarks = _bookmarkService.Load();
        _generalSettingsView = new GeneralSettingsView();
        _favoriteButtonsSettingsView = new FavoriteButtonsSettingsView();

        AttachEvents();
        LoadValuesToViews(_settings, _bookmarks);
        ShowGeneralSettings();
    }

    private void AttachEvents()
    {
        GeneralSettingsCategoryButton.Click += (_, _) => ShowGeneralSettings();
        FavoriteButtonsCategoryButton.Click += (_, _) => ShowFavoriteButtonsSettings();
        SaveSettingsButton.Click += (_, _) => SaveAndClose();
        CancelSettingsButton.Click += (_, _) => Close();
        ResetSettingsButton.Click += (_, _) => ResetFormValues();
        SettingsContentScrollViewer.PreviewMouseDown += SettingsContentScrollViewer_PreviewMouseDown;
        SettingsContentScrollViewer.PreviewMouseMove += SettingsContentScrollViewer_PreviewMouseMove;
        SettingsContentScrollViewer.PreviewMouseUp += SettingsContentScrollViewer_PreviewMouseUp;
        SettingsContentScrollViewer.PreviewMouseWheel += SettingsContentScrollViewer_PreviewMouseWheel;
        SettingsContentScrollViewer.LostMouseCapture += (_, _) => EndMiddleButtonScroll();
    }

    private void ShowGeneralSettings()
    {
        SettingsContentControl.Content = _generalSettingsView;
    }

    private void ShowFavoriteButtonsSettings()
    {
        _favoriteButtonsSettingsView.RefreshPlaybackOptionStates();
        SettingsContentControl.Content = _favoriteButtonsSettingsView;
    }

    private void LoadValuesToViews(AppSettings settings, IReadOnlyList<BookmarkItem> bookmarks)
    {
        _generalSettingsView.LoadSettings(settings);
        _favoriteButtonsSettingsView.LoadBookmarks(bookmarks);
    }

    public void SetCurrentWindowSize(double width, double height)
    {
        _generalSettingsView.SetCurrentWindowSize(width, height);
    }

    public bool ConfirmReloadBookmarksFromExternalChange()
    {
        var result = System.Windows.MessageBox.Show(
            Constants.AppConstants.BookmarksExternalChangedMessage,
            Constants.AppConstants.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public void ReloadBookmarksFromFile()
    {
        _bookmarks = _bookmarkService.Load();
        _favoriteButtonsSettingsView.LoadBookmarks(_bookmarks);
        _favoriteButtonsSettingsView.RefreshPlaybackOptionStates();
    }

    private void SettingsContentScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _middleScrollStartPoint = e.GetPosition(SettingsContentScrollViewer);
        _middleScrollStartOffset = SettingsContentScrollViewer.VerticalOffset;
        _isMiddleButtonScrolling = true;
        _previousCursor = Cursor;
        Cursor = System.Windows.Input.Cursors.SizeNS;
        SettingsContentScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void SettingsContentScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isMiddleButtonScrolling)
        {
            return;
        }

        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            EndMiddleButtonScroll();
            return;
        }

        var currentPoint = e.GetPosition(SettingsContentScrollViewer);
        var verticalDelta = currentPoint.Y - _middleScrollStartPoint.Y;
        SettingsContentScrollViewer.ScrollToVerticalOffset(_middleScrollStartOffset + verticalDelta);
        e.Handled = true;
    }

    private void SettingsContentScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndMiddleButtonScroll();
        e.Handled = true;
    }

    private void SettingsContentScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SettingsContentScrollViewer.ScrollToVerticalOffset(SettingsContentScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void EndMiddleButtonScroll()
    {
        if (!_isMiddleButtonScrolling)
        {
            return;
        }

        _isMiddleButtonScrolling = false;
        SettingsContentScrollViewer.ReleaseMouseCapture();
        Cursor = _previousCursor;
        _previousCursor = null;
    }

    private void SaveAndClose()
    {
        _generalSettingsView.CollectSettings(_settings);
        _generalSettingsView.ApplyDefaultWindowBounds(_settings);
        var bookmarks = _favoriteButtonsSettingsView.CollectBookmarks();

        _settingsService.Save(_settings);
        _bookmarkService.Save(bookmarks);

        DialogResult = true;
        Close();
    }

    private void ResetFormValues()
    {
        _generalSettingsView.ClearResetWindowBoundsRequest();
        LoadValuesToViews(_settingsService.CreateDefault(), _bookmarkService.CreateDefault());
        _generalSettingsView.MarkWindowSizeChanged();
        ShowGeneralSettings();
    }
}
