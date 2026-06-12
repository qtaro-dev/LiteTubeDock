using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;
using WpfBorder = System.Windows.Controls.Border;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LiteTubeDock;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly BookmarkService _bookmarkService = new();
    private readonly PlayerModeDiagnostics _playerModeDiagnostics = new();
    private readonly StartupOptions _startupOptions;
    private readonly WpfButton[] _favoriteButtons;
    private readonly DispatcherTimer _bookmarksReloadDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    private AppSettings _settings;
    private IReadOnlyList<BookmarkItem> _bookmarks = Array.Empty<BookmarkItem>();
    private FileSystemWatcher? _bookmarksWatcher;
    private SettingsWindow? _openSettingsWindow;
    private FullScreenSnapshot? _fullScreenSnapshot;
    private bool _skipNextNavigationConfirmation;
    private bool _isApplyingWindowSize;
    private bool _currentUrlIsTemporaryIpcNavigation;
    private readonly TaskCompletionSource<bool> _webViewReadyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private NamedPipeServerService? _namedPipeServer;
    private readonly bool _isPlayerMode;

    public MainWindow()
        : this(new StartupOptions())
    {
    }

    public MainWindow(StartupOptions startupOptions)
    {
        _startupOptions = startupOptions;
        _isPlayerMode = startupOptions.IsPlayerMode;

        InitializeComponent();

        _settings = _settingsService.Load();
        _favoriteButtons =
        [
            FavoriteButton01,
            FavoriteButton02,
            FavoriteButton03,
            FavoriteButton04,
            FavoriteButton05,
            FavoriteButton06,
            FavoriteButton07,
            FavoriteButton08,
            FavoriteButton09,
            FavoriteButton10
        ];

        RestoreWindowSettings();
        AddressTextBox.Text = _settings.RestoreLastUrl ? _settings.LastUrl : _settings.HomeUrl;
        AlwaysOnTopMenuItem.IsCheckable = true;
        ShowAddressBarMenuItem.IsCheckable = true;
        ShowNavigationButtonsMenuItem.IsCheckable = true;
        ApplyAlwaysOnTop(_settings.AlwaysOnTop);
        ApplyToolbarVisibility();
        ApplyStartupPlayerMode();
        UpdateWindowSizeMenuChecks(_settings.WindowSizePreset);
        _playerModeDiagnostics.RecordSettingsApplied(DateTime.Now, isImmediate: false);
        LoadBookmarks();
        AttachEvents();
        UpdateNavigationButtonStates();
        InitializeBookmarksWatcher();
        StartNamedPipeServer();
    }

    private void AttachEvents()
    {
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PlayerWebView.PreviewKeyDown += MainWindow_PreviewKeyDown;

        ImportBookmarksMenuItem.Click += (_, _) => ImportBookmarks();
        ExportBookmarksMenuItem.Click += (_, _) => ExportBookmarks();
        ExitMenuItem.Click += (_, _) => Close();
        HomeMenuItem.Click += (_, _) => NavigateTrusted(_settings.HomeUrl);
        BackMenuItem.Click += (_, _) => GoBack();
        ForwardMenuItem.Click += (_, _) => GoForward();
        ReloadMenuItem.Click += (_, _) => ReloadTrusted();
        HomeNavigationButton.Click += (_, _) => NavigateTrusted(_settings.HomeUrl);
        BackNavigationButton.Click += (_, _) => GoBack();
        ForwardNavigationButton.Click += (_, _) => GoForward();
        ReloadNavigationButton.Click += (_, _) => ReloadTrusted();
        AlwaysOnTopMenuItem.Click += (_, _) => ToggleAlwaysOnTop();
        ShowAddressBarMenuItem.Click += (_, _) => ToggleAddressBarVisibility();
        ShowNavigationButtonsMenuItem.Click += (_, _) => ToggleNavigationButtonsVisibility();
        WindowSize800x600MenuItem.Click += (_, _) => ApplyWindowSizePreset("800x600");
        WindowSize960x540MenuItem.Click += (_, _) => ApplyWindowSizePreset("960x540");
        WindowSize1024x768MenuItem.Click += (_, _) => ApplyWindowSizePreset("1024x768");
        WindowSize1280x720MenuItem.Click += (_, _) => ApplyWindowSizePreset("1280x720");
        WindowSize1600x900MenuItem.Click += (_, _) => ApplyWindowSizePreset("1600x900");
        WindowSize1920x1080MenuItem.Click += (_, _) => ApplyWindowSizePreset("1920x1080");
        WindowSize540x960MenuItem.Click += (_, _) => ApplyWindowSizePreset("540x960");
        WindowSize720x1280MenuItem.Click += (_, _) => ApplyWindowSizePreset("720x1280");
        WindowSize768x1024MenuItem.Click += (_, _) => ApplyWindowSizePreset("768x1024");
        WindowSize900x1600MenuItem.Click += (_, _) => ApplyWindowSizePreset("900x1600");
        WindowSize1080x1920MenuItem.Click += (_, _) => ApplyWindowSizePreset("1080x1920");
        WindowSizeCustomMenuItem.Click += (_, _) => ApplyWindowSizePreset(AppConstants.CustomWindowSizePreset);
        ResetWindowPositionMenuItem.Click += (_, _) => ResetWindowPosition();
        OpenSettingsMenuItem.Click += (_, _) => OpenSettingsWindow();
        ReloadSettingsMenuItem.Click += (_, _) => ReloadSettings(applyWindowSize: true);
        OpenHelpMenuItem.Click += (_, _) => OpenHelpWindow();
        PlayerModeDiagnosticsMenuItem.Click += (_, _) => ShowPlayerModeDiagnostics();
        AboutMenuItem.Click += (_, _) => OpenAboutWindow();
        NavigateButton.Click += (_, _) => NavigateFromAddressBar();
        CopyCurrentUrlButton.Click += (_, _) => CopyCurrentUrl();
        AddressTextBox.KeyDown += AddressTextBox_KeyDown;
        AddressTextBox.GotKeyboardFocus += AddressTextBox_GotKeyboardFocus;
        AddressTextBox.PreviewMouseLeftButtonDown += AddressTextBox_PreviewMouseLeftButtonDown;
        AddressTextBox.LostKeyboardFocus += AddressTextBox_LostKeyboardFocus;

        PlayerWebView.NavigationStarting += PlayerWebView_NavigationStarting;
        PlayerWebView.NavigationCompleted += PlayerWebView_NavigationCompleted;

        AttachFavoriteContextMenus();
    }

    private void AttachFavoriteContextMenus()
    {
        for (var index = 0; index < _favoriteButtons.Length; index++)
        {
            var menuItem = new MenuItem
            {
                Header = "現在再生中のムービーを登録",
                Tag = index
            };
            menuItem.Click += RegisterCurrentMovieMenuItem_Click;

            _favoriteButtons[index].ContextMenu = new ContextMenu
            {
                Items = { menuItem }
            };
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var userDataFolder = AppPathService.ResolveProjectPath(_settings.WebView2UserDataFolder);
            Directory.CreateDirectory(userDataFolder);

            var environmentOptions = _settings.EnableAutoplay
                ? new CoreWebView2EnvironmentOptions(AppConstants.AutoplayBrowserArgument)
                : null;
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: environmentOptions);

            await PlayerWebView.EnsureCoreWebView2Async(webViewEnvironment);
            PlayerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PlayerWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PlayerWebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            PlayerWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            PlayerWebView.CoreWebView2.HistoryChanged += (_, _) => UpdateNavigationButtonStates();
            ConfigureYouTubeEmbedReferer();
            UpdateNavigationButtonStates();
            _webViewReadyCompletion.TrySetResult(true);
            NavigateTrusted(GetInitialNavigationUrl());
        }
        catch (Exception)
        {
            _webViewReadyCompletion.TrySetResult(false);
            LoadingStatusText.Text = AppConstants.LoadingFailedText;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopNamedPipeServer();
        DisposeBookmarksWatcher();
        SaveCurrentSettings();
    }

    private void StartNamedPipeServer()
    {
        if (!_startupOptions.EnableIpc)
        {
            return;
        }

        try
        {
            var handler = new IpcCommandHandler(NavigateFromIpcAsync, GetIpcStatus);
            _namedPipeServer = new NamedPipeServerService(handler.HandleAsync);
            _namedPipeServer.Start(IpcConstants.GetPipeName(Environment.ProcessId));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Named pipe server start failed: {ex}");
        }
    }

    private void StopNamedPipeServer()
    {
        var server = _namedPipeServer;
        _namedPipeServer = null;
        if (server is null)
        {
            return;
        }

        try
        {
            _ = server.StopAsync();
            server.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Named pipe server dispose failed: {ex}");
        }
    }

    private async Task<bool> NavigateFromIpcAsync(string url, CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => NavigateFromIpcOnUiAsync(url, cancellationToken));
        return await await operation.Task;
    }

    private async Task<bool> NavigateFromIpcOnUiAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            if (!_webViewReadyCompletion.Task.IsCompleted)
            {
                await _webViewReadyCompletion.Task.WaitAsync(
                    TimeSpan.FromMilliseconds(IpcConstants.WebViewReadyTimeoutMilliseconds),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            Debug.WriteLine($"IPC navigate waited for WebView2 readiness and failed: {ex.Message}");
            return false;
        }

        if (PlayerWebView.CoreWebView2 is null)
        {
            return false;
        }

        NavigateTrusted(url, saveAsLastUrl: false);
        return true;
    }

    private IpcStatusData GetIpcStatus()
    {
        if (!Dispatcher.CheckAccess())
        {
            return Dispatcher.Invoke(GetIpcStatus);
        }

        return new IpcStatusData
        {
            ProcessId = Environment.ProcessId,
            WindowTitle = Title ?? string.Empty,
            IsPlayerMode = _isPlayerMode,
            CurrentUrl = GetCurrentWebViewUrl() ?? string.Empty,
            IsWebViewReady = PlayerWebView.CoreWebView2 is not null,
            AppVersion = AppConstants.AppVersion
        };
    }

    private void MainWindow_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.FocusedElement == AddressTextBox
            && key is not Key.Escape
            && !(Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Enter))
        {
            return;
        }

        if (HandleKeyCommand(key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    private void AddressTextBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        NavigateFromAddressBar();
        e.Handled = true;
    }

    private void AddressTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressTextBox.SelectAll();
    }

    private void AddressTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (AddressTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        e.Handled = true;
        AddressTextBox.Focus();
    }

    private void AddressTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus == NavigateButton)
        {
            return;
        }

        SyncAddressBarFromCurrentUrl();
    }

    private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        SyncCurrentUrlDisplaysFromWebView();
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;

        if (string.IsNullOrWhiteSpace(e.Uri))
        {
            LoadingStatusText.Text = AppConstants.NonVideoNavigationCancelledText;
            return;
        }

        if (ShouldConfirmNonVideoNavigation(e.Uri))
        {
            var result = System.Windows.MessageBox.Show(
                AppConstants.NonVideoNavigationConfirmMessage,
                AppConstants.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                LoadingStatusText.Text = AppConstants.NonVideoNavigationCancelledText;
                return;
            }
        }

        NavigateTrusted(e.Uri);
    }

    private void ConfigureYouTubeEmbedReferer()
    {
        try
        {
            PlayerWebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://www.youtube.com/embed/*",
                CoreWebView2WebResourceContext.All);
            PlayerWebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://www.youtube.com/iframe_api*",
                CoreWebView2WebResourceContext.All);
            PlayerWebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://www.youtube.com/s/player/*",
                CoreWebView2WebResourceContext.All);
            PlayerWebView.CoreWebView2.WebResourceRequested += PlayerWebView_WebResourceRequested;
        }
        catch (Exception)
        {
            LoadingStatusText.Text = AppConstants.WebViewRefererSetupFailedText;
        }
    }

    private void PlayerWebView_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (!ShouldAttachYouTubeReferer(e.Request.Uri))
            {
                _playerModeDiagnostics.RecordRefererSkipped(e.Request.Uri, "対象外");
                return;
            }

            if (!_settings.PlayerModeRefererEnabled)
            {
                _playerModeDiagnostics.RecordRefererSkipped(e.Request.Uri, "Referer付与OFF");
                return;
            }

            e.Request.Headers.SetHeader(AppConstants.RefererHeaderName, _settings.PlayerModeReferer);
            _playerModeDiagnostics.RecordRefererAttached(e.Request.Uri);
            LoadingStatusText.Text = AppConstants.WebViewRefererAttachedText;
        }
        catch (Exception ex)
        {
            _playerModeDiagnostics.RecordRefererFailed(e.Request.Uri, ex.Message);
            LoadingStatusText.Text = AppConstants.WebViewRefererSetupFailedText;
        }
    }

    private static bool ShouldAttachYouTubeReferer(string? requestUri)
    {
        if (string.IsNullOrWhiteSpace(requestUri)
            || !Uri.TryCreate(requestUri, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Equals("/iframe_api", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.StartsWith("/s/player/", StringComparison.OrdinalIgnoreCase);
    }

    private void PlayerWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (ShouldConfirmNonVideoNavigation(e.Uri))
        {
            var result = System.Windows.MessageBox.Show(
                AppConstants.NonVideoNavigationConfirmMessage,
                AppConstants.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                LoadingStatusText.Text = AppConstants.NonVideoNavigationCancelledText;
                return;
            }
        }

        LoadingStatusText.Text = AppConstants.LoadingStartingText;
        CurrentUrlText.Text = AppConstants.CurrentUrlPrefix + e.Uri;
        SyncAddressBarText(e.Uri);
    }

    private bool ShouldConfirmNonVideoNavigation(string? uri)
    {
        if (!_settings.ConfirmNonVideoNavigation)
        {
            _skipNextNavigationConfirmation = false;
            return false;
        }

        if (_skipNextNavigationConfirmation)
        {
            _skipNextNavigationConfirmation = false;
            return false;
        }

        return !IsAllowedYouTubeNavigation(uri);
    }

    private static bool IsAllowedYouTubeNavigation(string? requestUri)
    {
        if (string.IsNullOrWhiteSpace(requestUri)
            || !Uri.TryCreate(requestUri, UriKind.Absolute, out var uri)
            || uri.Scheme != "https")
        {
            return false;
        }

        return uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("m.youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    private void PlayerWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (PlayerWebView.Source is null)
        {
            CurrentUrlText.Text = AppConstants.CurrentUrlEmpty;
        }
        else
        {
            var currentUrl = PlayerWebView.Source.ToString();
            CurrentUrlText.Text = AppConstants.CurrentUrlPrefix + currentUrl;
            SyncAddressBarText(currentUrl);
        }

        LoadingStatusText.Text = e.IsSuccess
            ? AppConstants.LoadingCompletedText
            : AppConstants.LoadingFailedText;
        UpdateNavigationButtonStates();
    }

    private void LoadBookmarks()
    {
        ApplyBookmarks(_bookmarkService.Load());
    }

    private void ApplyBookmarks(IReadOnlyList<BookmarkItem> bookmarks)
    {
        _bookmarks = bookmarks;
        for (var index = 0; index < _favoriteButtons.Length; index++)
        {
            var button = _favoriteButtons[index];
            var bookmark = _bookmarks.ElementAtOrDefault(index);
            var isEnabled = bookmark is not null && bookmark.IsEnabled;

            button.Content = isEnabled ? CreateBookmarkButtonContent(bookmark!) : AppConstants.EmptyBookmarkLabel;
            button.IsEnabled = true;
            button.Opacity = isEnabled ? 1.0 : 0.55;
            button.Tag = isEnabled ? bookmark : null;
            ApplyBookmarkButtonColors(button, bookmark, isEnabled);

            button.Click -= FavoriteButton_Click;
            button.Click += FavoriteButton_Click;
        }
    }

    private void InitializeBookmarksWatcher()
    {
        try
        {
            var bookmarksDirectory = Path.GetDirectoryName(_bookmarkService.BookmarksFilePath);
            var bookmarksFileName = Path.GetFileName(_bookmarkService.BookmarksFilePath);
            if (string.IsNullOrWhiteSpace(bookmarksDirectory) || string.IsNullOrWhiteSpace(bookmarksFileName))
            {
                return;
            }

            Directory.CreateDirectory(bookmarksDirectory);
            _bookmarksReloadDebounceTimer.Tick += BookmarksReloadDebounceTimer_Tick;
            _bookmarksWatcher = new FileSystemWatcher(bookmarksDirectory, bookmarksFileName)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _bookmarksWatcher.Changed += BookmarksFileChanged;
            _bookmarksWatcher.Created += BookmarksFileChanged;
            _bookmarksWatcher.Renamed += BookmarksFileChanged;
            _bookmarksWatcher.Deleted += BookmarksFileChanged;
        }
        catch (Exception)
        {
            LoadingStatusText.Text = AppConstants.BookmarksReloadFailedText;
        }
    }

    private void BookmarksFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(RestartBookmarksReloadDebounce);
            return;
        }

        RestartBookmarksReloadDebounce();
    }

    private void RestartBookmarksReloadDebounce()
    {
        _bookmarksReloadDebounceTimer.Stop();
        _bookmarksReloadDebounceTimer.Start();
    }

    private async void BookmarksReloadDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _bookmarksReloadDebounceTimer.Stop();
        var bookmarks = await TryLoadBookmarksForWatcherAsync();
        if (bookmarks is null)
        {
            LoadingStatusText.Text = AppConstants.BookmarksReloadFailedText;
            return;
        }

        ApplyBookmarks(bookmarks);
        LoadingStatusText.Text = AppConstants.BookmarksReloadedText;

        if (_openSettingsWindow is { IsLoaded: true } settingsWindow
            && settingsWindow.ConfirmReloadBookmarksFromExternalChange())
        {
            settingsWindow.ReloadBookmarksFromFile();
        }
    }

    private async Task<IReadOnlyList<BookmarkItem>?> TryLoadBookmarksForWatcherAsync()
    {
        const int maxAttempts = 3;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var bookmarks = await Task.Run(() =>
                _bookmarkService.TryLoadExisting(out var loadedBookmarks)
                    ? loadedBookmarks
                    : null);

            if (bookmarks is not null)
            {
                return bookmarks;
            }

            await Task.Delay(200);
        }

        return null;
    }

    private void DisposeBookmarksWatcher()
    {
        _bookmarksReloadDebounceTimer.Stop();
        _bookmarksReloadDebounceTimer.Tick -= BookmarksReloadDebounceTimer_Tick;

        if (_bookmarksWatcher is null)
        {
            return;
        }

        _bookmarksWatcher.EnableRaisingEvents = false;
        _bookmarksWatcher.Changed -= BookmarksFileChanged;
        _bookmarksWatcher.Created -= BookmarksFileChanged;
        _bookmarksWatcher.Renamed -= BookmarksFileChanged;
        _bookmarksWatcher.Deleted -= BookmarksFileChanged;
        _bookmarksWatcher.Dispose();
        _bookmarksWatcher = null;
    }

    private static object CreateBookmarkButtonContent(BookmarkItem bookmark)
    {
        var icon = TryCreateBookmarkIcon(bookmark);
        var label = CreateBookmarkLabelTextBlock(bookmark);
        if (icon is null)
        {
            return label;
        }

        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(icon);
        panel.Children.Add(label);

        return panel;
    }

    private static TextBlock CreateBookmarkLabelTextBlock(BookmarkItem bookmark)
    {
        return new TextBlock
        {
            Text = CreateBookmarkDisplayLabel(bookmark.Label),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = bookmark.IsBold ? FontWeights.Bold : FontWeights.Normal,
            MaxWidth = 96
        };
    }

    private static string CreateBookmarkDisplayLabel(string? label)
    {
        var value = string.IsNullOrWhiteSpace(label)
            ? AppConstants.EmptyBookmarkLabel
            : label.Trim();

        return value.Length > AppConstants.FavoriteButtonDisplayLabelMaxLength
            ? value[..AppConstants.FavoriteButtonDisplayLabelMaxLength] + "..."
            : value;
    }

    private static WpfBorder? TryCreateBookmarkIcon(BookmarkItem bookmark)
    {
        if (!TryResolveBookmarkIconPath(bookmark.IconPath, out var fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.DecodePixelWidth = bookmark.IconShape == AppConstants.RectangleBookmarkIconShape ? 56 : 28;
            bitmap.DecodePixelHeight = 28;
            bitmap.EndInit();
            bitmap.Freeze();

            var imageBrush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill
            };

            return new WpfBorder
            {
                Background = imageBrush,
                Width = bookmark.IconShape == AppConstants.RectangleBookmarkIconShape ? 56 : 28,
                Height = 28,
                CornerRadius = bookmark.IconRounded
                    ? new CornerRadius(AppConstants.RoundedBookmarkIconCornerRadius)
                    : new CornerRadius(0),
                Margin = new Thickness(0, 0, 0, 2)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryResolveBookmarkIconPath(string? iconPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        fullPath = Path.IsPathRooted(iconPath)
            ? Path.GetFullPath(iconPath)
            : AppPathService.ResolveProjectPath(iconPath);

        return true;
    }

    private static void ApplyBookmarkButtonColors(WpfButton button, BookmarkItem? bookmark, bool isEnabled)
    {
        if (!isEnabled || bookmark is null)
        {
            button.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            button.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            return;
        }

        button.Background = CreateBrush(bookmark.BackgroundColor, AppConstants.DefaultBookmarkBackgroundColor);
        button.Foreground = CreateBrush(bookmark.ForegroundColor, AppConstants.DefaultBookmarkForegroundColor);
    }

    private static WpfBrush CreateBrush(string color, string fallback)
    {
        return TryCreateBrush(color) ?? TryCreateBrush(fallback) ?? System.Windows.Media.Brushes.Transparent;
    }

    private static WpfBrush? TryCreateBrush(string color)
    {
        try
        {
            return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: BookmarkItem bookmark })
        {
            NavigateBookmark(bookmark);
        }
    }

    private void RegisterCurrentMovieMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: int index })
        {
            return;
        }

        RegisterCurrentMovieToFavorite(index);
    }

    private void RegisterCurrentMovieToFavorite(int index)
    {
        var currentUrl = GetCurrentWebViewUrl();
        if (string.IsNullOrWhiteSpace(currentUrl)
            || !Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            LoadingStatusText.Text = AppConstants.FavoriteRegistrationFailedText;
            return;
        }

        var bookmarks = _bookmarks.ToList();
        while (bookmarks.Count < AppConstants.MaxBookmarks)
        {
            bookmarks.Add(new BookmarkItem { SortOrder = bookmarks.Count + 1 });
        }

        var bookmark = bookmarks[index];
        if (bookmark.IsEnabled && !string.IsNullOrWhiteSpace(bookmark.Url))
        {
            var result = System.Windows.MessageBox.Show(
                "すでに登録があります。登録してよろしいですか？",
                AppConstants.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        bookmark.Label = CreateFavoriteMovieLabel();
        bookmark.Url = uri.ToString();
        bookmark.SortOrder = index + 1;
        bookmark.IsEnabled = true;

        _bookmarkService.Save(bookmarks);
        LoadBookmarks();
        LoadingStatusText.Text = $"状態: 現在のムービーをお気に入り{index + 1:00}へ登録しました";
    }

    private string CreateFavoriteMovieLabel()
    {
        var title = PlayerWebView.CoreWebView2?.DocumentTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = AppConstants.DefaultFavoriteMovieLabel;
        }

        const string youtubeSuffix = " - YouTube";
        if (title.EndsWith(youtubeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^youtubeSuffix.Length].Trim();
        }

        if (title.Length > AppConstants.FavoriteMovieLabelMaxLength)
        {
            title = title[..AppConstants.FavoriteMovieLabelMaxLength];
        }

        return string.IsNullOrWhiteSpace(title)
            ? AppConstants.DefaultFavoriteMovieLabel
            : title;
    }

    private bool HandleKeyCommand(Key key, ModifierKeys modifiers)
    {
        if (IsDeveloperToolsShortcut(key, modifiers))
        {
            return true;
        }

        if (modifiers == ModifierKeys.Alt && key == Key.Enter)
        {
            ToggleFullScreen();
            return true;
        }

        if (key == Key.Escape && IsFullScreen)
        {
            ExitFullScreen();
            return true;
        }

        if (!_settings.EnableShortcutKeys)
        {
            return false;
        }

        if (modifiers == ModifierKeys.Control && key == Key.R)
        {
            ReloadTrusted();
            return true;
        }

        if (modifiers == ModifierKeys.None && TryGetBookmarkIndex(key, out var bookmarkIndex))
        {
            NavigateBookmark(bookmarkIndex);
            return true;
        }

        if (modifiers == ModifierKeys.Alt && key == Key.Left)
        {
            GoBack();
            return true;
        }

        if (modifiers == ModifierKeys.Alt && key == Key.Right)
        {
            GoForward();
            return true;
        }

        if (modifiers == ModifierKeys.Control && key == Key.H)
        {
            NavigateTrusted(_settings.HomeUrl);
            return true;
        }

        if (modifiers == ModifierKeys.Control && key == Key.Q)
        {
            Close();
            return true;
        }

        return false;
    }

    private static bool IsDeveloperToolsShortcut(Key key, ModifierKeys modifiers)
    {
        return (modifiers == ModifierKeys.None && key == Key.F12)
            || (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                && key is Key.I or Key.J);
    }

    private static bool TryGetBookmarkIndex(Key key, out int index)
    {
        index = key switch
        {
            Key.D1 or Key.NumPad1 or Key.F1 => 0,
            Key.D2 or Key.NumPad2 or Key.F2 => 1,
            Key.D3 or Key.NumPad3 or Key.F3 => 2,
            Key.D4 or Key.NumPad4 or Key.F4 => 3,
            Key.D5 or Key.NumPad5 or Key.F5 => 4,
            Key.D6 or Key.NumPad6 or Key.F6 => 5,
            Key.D7 or Key.NumPad7 or Key.F7 => 6,
            Key.D8 or Key.NumPad8 or Key.F8 => 7,
            Key.D9 or Key.NumPad9 or Key.F9 => 8,
            Key.D0 or Key.NumPad0 or Key.F10 => 9,
            _ => -1
        };

        return index >= 0;
    }

    private void NavigateBookmark(int index)
    {
        if (_bookmarks.ElementAtOrDefault(index) is { IsEnabled: true } bookmark)
        {
            NavigateBookmark(bookmark);
        }
    }

    private void NavigateBookmark(BookmarkItem bookmark)
    {
        var navigationUrl = FavoritePlaybackUrlService.GetNavigationUrl(bookmark);
        if (bookmark.PlaybackMode == AppConstants.PlayerPlaybackMode)
        {
            _playerModeDiagnostics.RecordPlayerUrl(
                bookmark.Url,
                navigationUrl,
                IsYouTubeEmbedUrl(navigationUrl));
        }

        NavigateTrusted(navigationUrl);
    }

    private static bool IsYouTubeEmbedUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase);
    }

    private void Navigate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _skipNextNavigationConfirmation = false;
            LoadingStatusText.Text = AppConstants.LoadingFailedText;
            return;
        }

        SyncAddressBarText(uri.ToString(), force: true);
        PlayerWebView.Source = uri;
    }

    private void NavigateTrusted(string url, bool saveAsLastUrl = true)
    {
        _currentUrlIsTemporaryIpcNavigation = !saveAsLastUrl;
        _skipNextNavigationConfirmation = true;
        Navigate(url);
    }

    private void ReloadTrusted()
    {
        if (string.IsNullOrWhiteSpace(GetCurrentWebViewUrl()))
        {
            return;
        }

        _skipNextNavigationConfirmation = true;
        PlayerWebView.Reload();
    }

    private void SyncAddressBarFromCurrentUrl()
    {
        SyncAddressBarText(GetCurrentWebViewUrl());
    }

    private void SyncCurrentUrlDisplaysFromWebView()
    {
        var currentUrl = GetCurrentWebViewUrl();
        if (string.IsNullOrWhiteSpace(currentUrl))
        {
            return;
        }

        CurrentUrlText.Text = AppConstants.CurrentUrlPrefix + currentUrl;
        SyncAddressBarText(currentUrl);
    }

    private string? GetCurrentWebViewUrl()
    {
        var currentUrl = PlayerWebView.CoreWebView2?.Source;
        if (string.IsNullOrWhiteSpace(currentUrl))
        {
            currentUrl = PlayerWebView.Source?.ToString();
        }

        return currentUrl;
    }

    private void SyncAddressBarText(string? url, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!force && Keyboard.FocusedElement == AddressTextBox)
        {
            return;
        }

        AddressTextBox.Text = url;
    }

    private void NavigateFromAddressBar()
    {
        if (!TryNormalizeAddressUrl(AddressTextBox.Text, out var normalizedUrl, out var statusText))
        {
            LoadingStatusText.Text = statusText;
            return;
        }

        if (!ConfirmAddressBarNavigation(normalizedUrl))
        {
            LoadingStatusText.Text = AppConstants.NonVideoNavigationCancelledText;
            return;
        }

        NavigateTrusted(normalizedUrl);
    }

    private bool ConfirmAddressBarNavigation(string normalizedUrl)
    {
        if (!_settings.ConfirmNonVideoNavigation || IsAllowedYouTubeNavigation(normalizedUrl))
        {
            return true;
        }

        var result = System.Windows.MessageBox.Show(
            AppConstants.NonVideoNavigationConfirmMessage,
            AppConstants.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static bool TryNormalizeAddressUrl(string? input, out string normalizedUrl, out string statusText)
    {
        normalizedUrl = string.Empty;
        statusText = AppConstants.AddressBarInvalidText;

        if (string.IsNullOrWhiteSpace(input))
        {
            statusText = AppConstants.AddressBarEmptyText;
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var candidate = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : "https://" + trimmed;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        statusText = string.Empty;
        return true;
    }

    private void CopyCurrentUrl()
    {
        var currentUrl = GetCurrentWebViewUrl();
        if (string.IsNullOrWhiteSpace(currentUrl))
        {
            LoadingStatusText.Text = AppConstants.CurrentUrlCopyFailedText;
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(currentUrl);
            LoadingStatusText.Text = AppConstants.CurrentUrlCopiedText;
        }
        catch (Exception)
        {
            LoadingStatusText.Text = AppConstants.CurrentUrlCopyFailedText;
        }
    }

    private void GoBack()
    {
        if (PlayerWebView.CanGoBack)
        {
            PlayerWebView.GoBack();
        }

        UpdateNavigationButtonStates();
    }

    private void GoForward()
    {
        if (PlayerWebView.CanGoForward)
        {
            PlayerWebView.GoForward();
        }

        UpdateNavigationButtonStates();
    }

    private void UpdateNavigationButtonStates()
    {
        var canGoBack = PlayerWebView.CanGoBack;
        var canGoForward = PlayerWebView.CanGoForward;
        var canReload = PlayerWebView.CoreWebView2 is not null || PlayerWebView.Source is not null;

        BackNavigationButton.IsEnabled = canGoBack;
        ForwardNavigationButton.IsEnabled = canGoForward;
        ReloadNavigationButton.IsEnabled = canReload;
        BackMenuItem.IsEnabled = canGoBack;
        ForwardMenuItem.IsEnabled = canGoForward;
        ReloadMenuItem.IsEnabled = canReload;
    }

    private void ToggleAlwaysOnTop()
    {
        ApplyAlwaysOnTop(!Topmost);
    }

    private void ApplyAlwaysOnTop(bool enabled)
    {
        Topmost = enabled;
        AlwaysOnTopMenuItem.IsChecked = enabled;
        AlwaysOnTopStatusText.Text = enabled
            ? AppConstants.AlwaysOnTopOnText
            : AppConstants.AlwaysOnTopOffText;
    }

    private void ToggleAddressBarVisibility()
    {
        ApplyAddressBarVisibility(!_settings.ShowAddressBar);
    }

    private void ApplyAddressBarVisibility(bool visible)
    {
        _settings.ShowAddressBar = visible;
        ApplyToolbarVisibility();
    }

    private void ToggleNavigationButtonsVisibility()
    {
        ApplyNavigationButtonsVisibility(!_settings.ShowNavigationButtons);
    }

    private void ApplyNavigationButtonsVisibility(bool visible)
    {
        _settings.ShowNavigationButtons = visible;
        ApplyToolbarVisibility();
    }

    private void ApplyToolbarVisibility()
    {
        ShowAddressBarMenuItem.IsChecked = _settings.ShowAddressBar;
        ShowNavigationButtonsMenuItem.IsChecked = _settings.ShowNavigationButtons;

        NavigationButtonPanel.Visibility = _settings.ShowNavigationButtons
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddressInputPanel.Visibility = _settings.ShowAddressBar
            ? Visibility.Visible
            : Visibility.Collapsed;

        AddressBarPanel.Visibility = !IsFullScreen && (_settings.ShowAddressBar || _settings.ShowNavigationButtons)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyStartupPlayerMode()
    {
        if (!_isPlayerMode)
        {
            return;
        }

        TopMenu.Visibility = Visibility.Collapsed;
        AddressBarPanel.Visibility = Visibility.Collapsed;
        FavoriteButtonPanel.Visibility = Visibility.Collapsed;
        StatusBarArea.Visibility = Visibility.Collapsed;
        ToolbarRow.Height = new GridLength(0);
        FavoriteButtonRow.Height = new GridLength(0);
        StatusBarRow.Height = new GridLength(0);
        PlayerArea.Margin = new Thickness(0);
        PlayerArea.BorderThickness = new Thickness(0);
    }

    private string GetInitialNavigationUrl()
    {
        if (_startupOptions.HasInitialUrl)
        {
            return _startupOptions.InitialUrl!;
        }

        return _settings.RestoreLastUrl ? _settings.LastUrl : _settings.HomeUrl;
    }

    private void OpenSettingsWindow()
    {
        var settingsWindow = new SettingsWindow(_settingsService, _bookmarkService)
        {
            Owner = this
        };
        var currentWindowSize = GetCurrentWindowSizeForSettings();
        settingsWindow.SetCurrentWindowSize(currentWindowSize.Width, currentWindowSize.Height);

        _openSettingsWindow = settingsWindow;
        try
        {
            if (settingsWindow.ShowDialog() == true)
            {
                ReloadSettings(
                    settingsWindow.ResetWindowBoundsRequested,
                    settingsSaved: true,
                    applyWindowSize: settingsWindow.ResetWindowBoundsRequested || settingsWindow.WindowSizeChanged);
            }
        }
        finally
        {
            if (ReferenceEquals(_openSettingsWindow, settingsWindow))
            {
                _openSettingsWindow = null;
            }
        }
    }

    private void ExportBookmarks()
    {
        try
        {
            var exportFolder = GetSettingsExportFolderPath();
            Directory.CreateDirectory(exportFolder);

            using var dialog = new Forms.SaveFileDialog
            {
                Title = "お気に入り設定をエクスポート",
                InitialDirectory = exportFolder,
                FileName = AppConstants.DefaultBookmarksExportFileName,
                Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            _bookmarkService.ExportToFile(dialog.FileName);
            LoadingStatusText.Text = AppConstants.BookmarksExportedText;
        }
        catch (Exception)
        {
            LoadingStatusText.Text = AppConstants.BookmarksExportFailedText;
            System.Windows.MessageBox.Show(
                AppConstants.BookmarksExportFailedText,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportBookmarks()
    {
        try
        {
            var exportFolder = GetSettingsExportFolderPath();
            Directory.CreateDirectory(exportFolder);

            using var dialog = new Forms.OpenFileDialog
            {
                Title = "お気に入り設定をインポート",
                InitialDirectory = exportFolder,
                Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                AppConstants.ImportBookmarksConfirmMessage,
                AppConstants.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            BackupBookmarks(exportFolder);
            _bookmarks = _bookmarkService.ImportFromFile(dialog.FileName);
            LoadBookmarks();
            LoadingStatusText.Text = AppConstants.BookmarksImportedText;
        }
        catch (Exception)
        {
            LoadingStatusText.Text = AppConstants.BookmarksImportFailedText;
            System.Windows.MessageBox.Show(
                AppConstants.BookmarksImportFailedText,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BackupBookmarks(string exportFolder)
    {
        Directory.CreateDirectory(exportFolder);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(exportFolder, $"bookmarks_backup_{timestamp}.json");
        File.Copy(_bookmarkService.BookmarksFilePath, backupPath, overwrite: false);
    }

    private string GetSettingsExportFolderPath()
    {
        return AppPathService.ResolveSettingsExportFolder(_settings.SettingsExportFolder);
    }

    private void OpenHelpWindow()
    {
        var helpWindow = new HelpWindow
        {
            Owner = this
        };

        helpWindow.ShowDialog();
    }

    private void OpenAboutWindow()
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };

        aboutWindow.ShowDialog();
    }

    private void ShowPlayerModeDiagnostics()
    {
        System.Windows.MessageBox.Show(
            _playerModeDiagnostics.ToDisplayText(_settings.PlayerModeRefererEnabled, _settings.PlayerModeReferer),
            AppConstants.PlayerModeDiagnosticsTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ReloadSettings(bool applyWindowBounds = false, bool settingsSaved = false, bool applyWindowSize = false)
    {
        _settings = _settingsService.Load();
        ApplyAlwaysOnTop(_settings.AlwaysOnTop);
        ApplyToolbarVisibility();
        _playerModeDiagnostics.RecordSettingsApplied(DateTime.Now, settingsSaved);
        if (applyWindowBounds)
        {
            ApplySavedWindowBounds();
        }
        else if (applyWindowSize)
        {
            ApplySavedWindowSize();
        }

        UpdateWindowSizeMenuChecks(applyWindowBounds || applyWindowSize
            ? _settings.WindowSizePreset
            : GetCurrentWindowSizePresetForMenu());
        LoadBookmarks();
        LoadingStatusText.Text = applyWindowBounds
            ? AppConstants.WindowBoundsResetText
            : settingsSaved
                ? AppConstants.SettingsSavedAppliedText
                : AppConstants.SettingsReloadedText;
    }

    private void ApplySavedWindowSize()
    {
        if (IsFullScreen)
        {
            ExitFullScreen();
        }

        SetWindowSize(_settings.Window.Width, _settings.Window.Height);
    }

    private void ApplySavedWindowBounds()
    {
        ApplySavedWindowSize();

        Left = _settings.Window.Left;
        Top = _settings.Window.Top;

        if (!IsWindowPositionVisible(Left, Top))
        {
            var position = GetSafeWindowPosition();
            Left = position.Left;
            Top = position.Top;
            _settings.Window.Left = Left;
            _settings.Window.Top = Top;
        }
    }

    private void ApplyWindowSizePreset(string preset)
    {
        if (IsFullScreen)
        {
            ExitFullScreen();
        }

        if (!AppConstants.TryGetWindowSizePreset(preset, out var width, out var height))
        {
            var currentSize = GetCurrentWindowSizeForSettings();
            width = currentSize.Width;
            height = currentSize.Height;
            preset = AppConstants.CustomWindowSizePreset;
        }

        SetWindowSize(width, height);
        _settings.Window.Width = width;
        _settings.Window.Height = height;
        _settings.WindowSizePreset = preset;
        _settingsService.Save(_settings);
        UpdateWindowSizeMenuChecks(preset);
        LoadingStatusText.Text = AppConstants.WindowSizeChangedText;
    }

    private void SetWindowSize(double width, double height)
    {
        _isApplyingWindowSize = true;
        try
        {
            Width = width;
            Height = height;
        }
        finally
        {
            _isApplyingWindowSize = false;
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isApplyingWindowSize || IsFullScreen)
        {
            return;
        }

        UpdateWindowSizeMenuChecks(GetCurrentWindowSizePresetForMenu());
    }

    private void ResetWindowPosition()
    {
        var position = GetSafeWindowPosition();
        Left = position.Left;
        Top = position.Top;
        _settings.Window.Left = Left;
        _settings.Window.Top = Top;
        LoadingStatusText.Text = AppConstants.WindowPositionResetText;
    }

    private void UpdateWindowSizeMenuChecks(string preset)
    {
        WindowSize800x600MenuItem.IsCheckable = true;
        WindowSize960x540MenuItem.IsCheckable = true;
        WindowSize1024x768MenuItem.IsCheckable = true;
        WindowSize1280x720MenuItem.IsCheckable = true;
        WindowSize1600x900MenuItem.IsCheckable = true;
        WindowSize1920x1080MenuItem.IsCheckable = true;
        WindowSize540x960MenuItem.IsCheckable = true;
        WindowSize720x1280MenuItem.IsCheckable = true;
        WindowSize768x1024MenuItem.IsCheckable = true;
        WindowSize900x1600MenuItem.IsCheckable = true;
        WindowSize1080x1920MenuItem.IsCheckable = true;
        WindowSizeCustomMenuItem.IsCheckable = true;

        WindowSize800x600MenuItem.IsChecked = preset == "800x600";
        WindowSize960x540MenuItem.IsChecked = preset == "960x540";
        WindowSize1024x768MenuItem.IsChecked = preset == "1024x768";
        WindowSize1280x720MenuItem.IsChecked = preset == "1280x720";
        WindowSize1600x900MenuItem.IsChecked = preset == "1600x900";
        WindowSize1920x1080MenuItem.IsChecked = preset == "1920x1080";
        WindowSize540x960MenuItem.IsChecked = preset == "540x960";
        WindowSize720x1280MenuItem.IsChecked = preset == "720x1280";
        WindowSize768x1024MenuItem.IsChecked = preset == "768x1024";
        WindowSize900x1600MenuItem.IsChecked = preset == "900x1600";
        WindowSize1080x1920MenuItem.IsChecked = preset == "1080x1920";
        WindowSizeCustomMenuItem.IsChecked = preset == AppConstants.CustomWindowSizePreset;
    }

    private string GetCurrentWindowSizePresetForMenu()
    {
        var size = GetCurrentWindowSizeForSettings();
        return AppConstants.GetWindowSizePresetForSize(size.Width, size.Height);
    }

    private bool IsFullScreen => _fullScreenSnapshot is not null;

    private void ToggleFullScreen()
    {
        if (IsFullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        if (IsFullScreen)
        {
            return;
        }

        _fullScreenSnapshot = new FullScreenSnapshot(
            Left,
            Top,
            Width,
            Height,
            WindowStyle,
            WindowState,
            ResizeMode,
            TopMenu.Visibility,
            AddressBarPanel.Visibility,
            FavoriteButtonPanel.Visibility,
            StatusBarArea.Visibility,
            PlayerArea.Margin,
            PlayerArea.BorderThickness);

        TopMenu.Visibility = Visibility.Collapsed;
        AddressBarPanel.Visibility = Visibility.Collapsed;
        FavoriteButtonPanel.Visibility = Visibility.Collapsed;
        StatusBarArea.Visibility = Visibility.Collapsed;
        PlayerArea.Margin = new Thickness(0);
        PlayerArea.BorderThickness = new Thickness(0);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        LoadingStatusText.Text = AppConstants.FullScreenOnText;
    }

    private void ExitFullScreen()
    {
        if (_fullScreenSnapshot is not { } snapshot)
        {
            return;
        }

        WindowState = WindowState.Normal;
        WindowStyle = snapshot.WindowStyle;
        ResizeMode = snapshot.ResizeMode;
        Left = snapshot.Left;
        Top = snapshot.Top;
        Width = snapshot.Width;
        Height = snapshot.Height;
        WindowState = snapshot.WindowState;

        _fullScreenSnapshot = null;

        TopMenu.Visibility = snapshot.TopMenuVisibility;
        ApplyToolbarVisibility();
        FavoriteButtonPanel.Visibility = snapshot.FavoriteButtonPanelVisibility;
        StatusBarArea.Visibility = snapshot.StatusBarAreaVisibility;
        PlayerArea.Margin = snapshot.PlayerAreaMargin;
        PlayerArea.BorderThickness = snapshot.PlayerAreaBorderThickness;

        LoadingStatusText.Text = AppConstants.FullScreenOffText;
    }

    private void RestoreWindowSettings()
    {
        if (!_settings.RestoreWindowState)
        {
            return;
        }

        Left = _settings.Window.Left;
        Top = _settings.Window.Top;
        SetWindowSize(_settings.Window.Width, _settings.Window.Height);

        if (!IsWindowPositionVisible(Left, Top))
        {
            var position = GetSafeWindowPosition();
            Left = position.Left;
            Top = position.Top;
            _settings.Window.Left = Left;
            _settings.Window.Top = Top;
        }
    }

    private static bool IsWindowPositionVisible(double left, double top)
    {
        return Forms.Screen.AllScreens.Any(screen =>
            left >= screen.WorkingArea.Left
            && left <= screen.WorkingArea.Right
            && top >= screen.WorkingArea.Top
            && top <= screen.WorkingArea.Bottom);
    }

    private static (double Left, double Top) GetSafeWindowPosition()
    {
        var workingArea = Forms.Screen.PrimaryScreen?.WorkingArea;
        if (workingArea is null)
        {
            return (AppConstants.DefaultWindowLeft, AppConstants.DefaultWindowTop);
        }

        return (
            workingArea.Value.Left + AppConstants.DefaultWindowLeft,
            workingArea.Value.Top + AppConstants.DefaultWindowTop);
    }

    private void SaveCurrentSettings()
    {
        if (!_startupOptions.HasInitialUrl && !_currentUrlIsTemporaryIpcNavigation)
        {
            _settings.LastUrl = GetCurrentWebViewUrl() ?? _settings.LastUrl;
        }

        _settings.AlwaysOnTop = Topmost;
        _settings.ShowAddressBar = ShowAddressBarMenuItem.IsChecked;
        _settings.ShowNavigationButtons = ShowNavigationButtonsMenuItem.IsChecked;

        if (_fullScreenSnapshot is { } snapshot)
        {
            _settings.Window.Left = snapshot.Left;
            _settings.Window.Top = snapshot.Top;
            _settings.Window.Width = snapshot.Width;
            _settings.Window.Height = snapshot.Height;
        }
        else if (WindowState != WindowState.Minimized)
        {
            _settings.Window.Left = Left;
            _settings.Window.Top = Top;
            _settings.Window.Width = Width;
            _settings.Window.Height = Height;
            _settings.WindowSizePreset = AppConstants.GetWindowSizePresetForSize(Width, Height);
        }

        _settingsService.Save(_settings);
    }

    private (double Width, double Height) GetCurrentWindowSizeForSettings()
    {
        if (_fullScreenSnapshot is { } snapshot)
        {
            return (snapshot.Width, snapshot.Height);
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            width = _settings.Window.Width;
        }

        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
        {
            height = _settings.Window.Height;
        }

        return (Math.Round(width), Math.Round(height));
    }

    private sealed record FullScreenSnapshot(
        double Left,
        double Top,
        double Width,
        double Height,
        WindowStyle WindowStyle,
        WindowState WindowState,
        ResizeMode ResizeMode,
        Visibility TopMenuVisibility,
        Visibility AddressBarPanelVisibility,
        Visibility FavoriteButtonPanelVisibility,
        Visibility StatusBarAreaVisibility,
        Thickness PlayerAreaMargin,
        Thickness PlayerAreaBorderThickness);
}
