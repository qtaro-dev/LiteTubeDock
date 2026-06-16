using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiteTubeDock.Constants;
using LiteTubeDock.Interop;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using LiteTubeDock.Views;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LiteTubeDock;

public partial class MainWindow : Window
{
    private const int InitialStartPausedMaxAttempts = 10;
    private const int InitialStartPausedRetryDelayMilliseconds = 500;
    private const int FavoritePlaybackMaxAttempts = 40;
    private const int FavoritePlaybackRetryDelayMilliseconds = 500;
    private const int FavoriteSeekVerifyMaxAttempts = 8;
    private const int FavoriteSeekVerifyRetryDelayMilliseconds = 250;
    private const double FavoriteSeekToleranceSeconds = 2.0;
    private const int ResizeDiagnosticsDebounceMilliseconds = 400;
    private const long PlayerModeRemoveStyleMask =
        NativeMethods.WsCaption
        | NativeMethods.WsBorder
        | NativeMethods.WsDlgFrame
        | NativeMethods.WsSysMenu
        | NativeMethods.WsMinimizeBox
        | NativeMethods.WsMaximizeBox
        | NativeMethods.WsThickFrame;
    private const long PlayerModeRemoveExStyleMask =
        NativeMethods.WsExDlgModalFrame
        | NativeMethods.WsExClientEdge
        | NativeMethods.WsExWindowEdge;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    private readonly SettingsService _settingsService = new();
    private readonly BookmarkService _bookmarkService = new();
    private readonly PlayerModeDiagnostics _playerModeDiagnostics = new();
    private readonly StartupOptions _startupOptions;
    private readonly WpfButton[] _favoriteButtons;
    private readonly DispatcherTimer _bookmarksReloadDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };
    private readonly DispatcherTimer _resizeDiagnosticsDebounceTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(ResizeDiagnosticsDebounceMilliseconds)
    };

    private AppSettings _settings;
    private IReadOnlyList<BookmarkItem> _bookmarks = Array.Empty<BookmarkItem>();
    private FileSystemWatcher? _bookmarksWatcher;
    private SettingsWindow? _openSettingsWindow;
    private LogWindow? _openLogWindow;
    private FullScreenSnapshot? _fullScreenSnapshot;
    private bool _skipNextNavigationConfirmation;
    private bool _isApplyingWindowSize;
    private bool _currentUrlIsTemporaryIpcNavigation;
    private readonly TaskCompletionSource<bool> _webViewReadyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private NamedPipeServerService? _namedPipeServer;
    private bool _pipeServerStarted;
    private string _ipcPipeName = string.Empty;
    private string? _lastIpcError;
    private bool _navigationCompleted;
    private MediaControlResult? _lastMediaControlResult;
    private string _lastMediaCommand = string.Empty;
    private WindowTransitionSnapshot? _pendingResizeSnapshot;
    private bool _isFullScreenTransitioning;
    private bool _navigationStartedDuringWindowTransition;
    private bool _navigationCompletedDuringWindowTransition;
    private readonly bool _isPlayerMode;
    private bool _initialStartPausedScheduled;
    private bool _initialStartPausedCompleted;
    private bool _userPlayCommandReceived;
    private bool _mutePersistenceEnabled;
    private int? _desiredVolumePercent;
    private bool? _desiredMutedState;
    private string _lastMuteReapplyReason = string.Empty;
    private FavoritePlaybackRequest? _pendingFavoritePlaybackRequest;
    private CancellationTokenSource? _favoritePlaybackCancellation;

    public MainWindow()
        : this(new StartupOptions())
    {
    }

    public MainWindow(StartupOptions startupOptions)
    {
        _startupOptions = startupOptions;
        _isPlayerMode = startupOptions.IsPlayerMode;
        _mutePersistenceEnabled = startupOptions.KeepMuted;
        ApplyPlayerModeChromeBeforeInitialize();

        InitializeComponent();

        _settings = _settingsService.Load();
        LogMenuItem.Header = AppConstants.LogMenuText;
        OpenLogWindowMenuItem.Header = AppConstants.LogViewerMenuText;
        OpenLogFolderMenuItem.Header = AppConstants.OpenLogFolderMenuText;
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

    private void ApplyPlayerModeChromeBeforeInitialize()
    {
        if (!_isPlayerMode)
        {
            DiagnosticLogService.Write(
                AppConstants.WindowStateLogCategory,
                CreatePlayerModeChromeLog("PreInitializeSkipped", null, "NotPlayerMode"));
            return;
        }

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        DiagnosticLogService.Write(
            AppConstants.WindowStateLogCategory,
            CreatePlayerModeChromeLog("PreInitializeApplied", null, string.Empty));
    }

    private void AttachEvents()
    {
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        LocationChanged += MainWindow_LocationChanged;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PlayerWebView.PreviewKeyDown += MainWindow_PreviewKeyDown;
        PlayerModeDragArea.MouseLeftButtonDown += PlayerModeDragArea_MouseLeftButtonDown;

        ImportBookmarksMenuItem.Click += (_, _) => ImportBookmarks();
        ExportBookmarksMenuItem.Click += (_, _) => ExportBookmarks();
        OpenLogWindowMenuItem.Click += (_, _) => OpenLogWindow();
        OpenLogFolderMenuItem.Click += (_, _) => OpenLogFolder();
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
        _resizeDiagnosticsDebounceTimer.Tick += ResizeDiagnosticsDebounceTimer_Tick;

        AttachFavoriteContextMenus();
    }

    private void AttachFavoriteContextMenus()
    {
        for (var index = 0; index < _favoriteButtons.Length; index++)
        {
            var contextMenu = new ContextMenu
            {
                Tag = index
            };
            contextMenu.Opened += FavoriteButtonContextMenu_Opened;
            _favoriteButtons[index].ContextMenu = contextMenu;
        }
    }

    private void FavoriteButtonContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { Tag: int index } contextMenu)
        {
            return;
        }

        contextMenu.Items.Clear();
        var shiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (shiftPressed)
        {
            var menuItem = new MenuItem
            {
                Header = AppConstants.FavoriteResetToHomeMenuText,
                Foreground = System.Windows.Media.Brushes.Red,
                Tag = index,
                IsEnabled = IsFavoriteRegisteredForReset(index)
            };
            menuItem.Click += ResetFavoriteToHomeMenuItem_Click;
            contextMenu.Items.Add(menuItem);
            LogFavoriteResetContextMenu(index, shiftPressed, "ResetMenuShown", confirmed: null, saveResult: string.Empty, result: "MenuShown", errorCode: string.Empty);
            return;
        }

        var registerItem = new MenuItem
        {
            Header = "現在再生中のムービーを登録",
            Tag = index
        };
        registerItem.Click += RegisterCurrentMovieMenuItem_Click;
        contextMenu.Items.Add(registerItem);
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
        _resizeDiagnosticsDebounceTimer.Stop();
        _resizeDiagnosticsDebounceTimer.Tick -= ResizeDiagnosticsDebounceTimer_Tick;
        CloseLogWindow();
        StopNamedPipeServer();
        DisposeBookmarksWatcher();
        SaveCurrentSettings();
    }

    private void OpenLogWindow()
    {
        if (_openLogWindow is { IsVisible: true })
        {
            _openLogWindow.Activate();
            return;
        }

        var logWindow = new LogWindow
        {
            Owner = this
        };
        logWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openLogWindow, logWindow))
            {
                _openLogWindow = null;
            }
        };
        _openLogWindow = logWindow;
        logWindow.Show();
    }

    private void CloseLogWindow()
    {
        var logWindow = _openLogWindow;
        _openLogWindow = null;
        logWindow?.Close();
    }

    private static void OpenLogFolder()
    {
        try
        {
            new LogFileService().OpenLogsFolder();
        }
        catch (Exception)
        {
            System.Windows.MessageBox.Show(
                AppConstants.LogViewerOpenFolderFailedText,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void StartNamedPipeServer()
    {
        if (!_startupOptions.IsIpcEnabled)
        {
            DiagnosticLogService.Write("IPC", "Start skipped.");
            DiagnosticLogService.Write("IPC", "Reason: UseIpcEnabled is false.");
            return;
        }

        _ipcPipeName = IpcConstants.GetPipeName(Environment.ProcessId);
        DiagnosticLogService.Write("IPC", "Start requested.");
        DiagnosticLogService.Write("IPC", $"PipeName: {_ipcPipeName}");
        DiagnosticLogService.Write("IPC", "CurrentUserOnly: True");

        try
        {
            var handler = new IpcCommandHandler(
                NavigateFromIpcAsync,
                ControlMediaFromIpcAsync,
                GetAudioStatusFromIpcAsync,
                SetVolumeFromIpcAsync,
                SetMutedFromIpcAsync,
                SeekToFromIpcAsync,
                ControlInlineFullscreenFromIpcAsync,
                SetMutePersistenceFromIpcAsync,
                GetMutePersistenceFromIpcAsync,
                GetIpcStatusAsync,
                PlayerControlFromIpcAsync,
                message => DiagnosticLogService.Write("IPC", message),
                RecordIpcError);
            _namedPipeServer = new NamedPipeServerService(
                handler.HandleAsync,
                message => DiagnosticLogService.Write("IPC", message),
                RecordIpcError);
            _namedPipeServer.Start(_ipcPipeName);
            _pipeServerStarted = true;
            RecordIpcError(null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Named pipe server start failed: {ex}");
            _pipeServerStarted = false;
            RecordIpcError(ex.Message);
            DiagnosticLogService.WriteException("IPC", "Start result: Failed", ex);
            LoadingStatusText.Text = AppConstants.IpcStartFailedText;
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
            _pipeServerStarted = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Named pipe server dispose failed: {ex}");
            RecordIpcError(ex.Message);
            DiagnosticLogService.WriteException("IPC", "Dispose failed.", ex);
        }
    }

    private void RecordIpcError(string? message)
    {
        _lastIpcError = string.IsNullOrWhiteSpace(message) ? null : message;
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

    private async Task<MediaControlResult> ControlMediaFromIpcAsync(string command, CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => ControlMediaFromIpcOnUiAsync(command, cancellationToken));
        return await await operation.Task;
    }

    private async Task<MediaControlResult> ControlMediaFromIpcOnUiAsync(string command, CancellationToken cancellationToken)
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
            Debug.WriteLine($"IPC media control waited for WebView2 readiness and failed: {ex.Message}");
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage, ex);
        }

        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var playerResult = await ExecuteUnifiedPlayerControlOnUiAsync(
            command,
            positionSeconds: null,
            volumePercent: null,
            muted: null,
            controlPolicy: null,
            cancellationToken);
        var result = ToMediaControlResult(playerResult);
        _lastMediaCommand = command;
        _lastMediaControlResult = result;
        LogMediaControlResult(command, result);
        if (command.Equals(IpcConstants.CommandToggleMute, StringComparison.OrdinalIgnoreCase)
            && result.Success
            && result.MediaFound
            && result.IsMuted.HasValue)
        {
            _desiredMutedState = result.IsMuted;
            if (_mutePersistenceEnabled)
            {
                var muteResult = await ApplyMutePersistenceOnUiAsync("ipc-toggle-mute", cancellationToken);
                LogMutePersistenceResult("ipc-toggle-mute", muteResult);
            }
        }

        return result;
    }

    private async Task<MutePersistenceResult> SetMutePersistenceFromIpcAsync(
        bool? enabled,
        CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => SetMutePersistenceFromIpcOnUiAsync(enabled, cancellationToken));
        return await await operation.Task;
    }

    private async Task<MutePersistenceResult> SetMutePersistenceFromIpcOnUiAsync(
        bool? enabled,
        CancellationToken cancellationToken)
    {
        if (!enabled.HasValue)
        {
            return new MutePersistenceResult
            {
                Success = false,
                ErrorCode = IpcConstants.ErrorCodeMutePersistenceSetFailed,
                Message = "Mute persistence enabled value is required.",
                CurrentUrl = GetCurrentWebViewUrl(),
                MutePersistenceEnabled = _mutePersistenceEnabled,
                DesiredMutedState = _desiredMutedState
            };
        }

        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var before = await MutePersistenceService.InspectAsync(
            PlayerWebView.CoreWebView2,
            _mutePersistenceEnabled,
            _desiredMutedState,
            cancellationToken);
        _mutePersistenceEnabled = enabled.Value;
        if (enabled.Value)
        {
            _desiredMutedState = before.ActualMutedState ?? _desiredMutedState;
        }

        var result = await MutePersistenceService.SetAsync(
            PlayerWebView.CoreWebView2,
            _mutePersistenceEnabled,
            _desiredMutedState,
            "ipc-set",
            cancellationToken);
        UpdateMutePersistenceState(result);
        LogMutePersistenceResult(IpcConstants.CommandSetMutePersistence, result);
        return result;
    }

    private async Task<MutePersistenceResult> GetMutePersistenceFromIpcAsync(CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => GetMutePersistenceFromIpcOnUiAsync(cancellationToken));
        return await await operation.Task;
    }

    private async Task<MutePersistenceResult> GetMutePersistenceFromIpcOnUiAsync(CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var result = await MutePersistenceService.InspectAsync(
            PlayerWebView.CoreWebView2,
            _mutePersistenceEnabled,
            _desiredMutedState,
            cancellationToken);
        UpdateMutePersistenceState(result);
        LogMutePersistenceResult(IpcConstants.CommandGetMutePersistence, result);
        return result;
    }

    private async Task WaitForWebViewReadyForIpcAsync(CancellationToken cancellationToken)
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
            Debug.WriteLine($"IPC waited for WebView2 readiness and failed: {ex.Message}");
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage, ex);
        }
    }

    private async Task<AudioControlResult> GetAudioStatusFromIpcAsync(CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => GetAudioStatusFromIpcOnUiAsync(cancellationToken));
        return await await operation.Task;
    }

    private async Task<AudioControlResult> GetAudioStatusFromIpcOnUiAsync(CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var playerResult = await ExecuteUnifiedPlayerControlOnUiAsync(
            IpcConstants.CommandPlayerGetState,
            positionSeconds: null,
            volumePercent: null,
            muted: null,
            controlPolicy: null,
            cancellationToken);
        var result = ToAudioControlResult(playerResult);
        LogAudioControlResult(IpcConstants.CommandGetAudioStatus, result, detailed: false);
        return result;
    }

    private async Task<AudioControlResult> SetVolumeFromIpcAsync(int volumePercent, CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => SetVolumeFromIpcOnUiAsync(volumePercent, cancellationToken));
        return await await operation.Task;
    }

    private async Task<AudioControlResult> SetVolumeFromIpcOnUiAsync(int volumePercent, CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var playerResult = await ExecuteUnifiedPlayerControlOnUiAsync(
            IpcConstants.CommandPlayerSetVolume,
            positionSeconds: null,
            volumePercent,
            muted: null,
            controlPolicy: null,
            cancellationToken);
        var result = ToAudioControlResult(playerResult);
        if (result.Success && result.MediaFound)
        {
            _desiredVolumePercent = result.AppliedVolumePercent ?? result.VolumePercent ?? volumePercent;
            result = WithDesiredAudioState(result, _desiredVolumePercent, _desiredMutedState);
        }

        LogAudioControlResult(IpcConstants.CommandSetVolume, result, detailed: true);
        return result;
    }

    private async Task<AudioControlResult> SetMutedFromIpcAsync(bool muted, CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => SetMutedFromIpcOnUiAsync(muted, cancellationToken));
        return await await operation.Task;
    }

    private async Task<AudioControlResult> SetMutedFromIpcOnUiAsync(bool muted, CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var playerResult = await ExecuteUnifiedPlayerControlOnUiAsync(
            IpcConstants.CommandPlayerSetMuted,
            positionSeconds: null,
            volumePercent: null,
            muted,
            controlPolicy: null,
            cancellationToken);
        var result = ToAudioControlResult(playerResult);
        if (result.Success && result.MediaFound)
        {
            _desiredMutedState = result.ActualMuted ?? muted;
            _mutePersistenceEnabled = _desiredMutedState == true;
            result = WithDesiredAudioState(result, _desiredVolumePercent, _desiredMutedState);
            var muteResult = await ApplyMutePersistenceOnUiAsync("ipc-set-muted", cancellationToken);
            LogMutePersistenceResult("ipc-set-muted", muteResult);
        }

        LogAudioControlResult(IpcConstants.CommandSetMuted, result, detailed: true);
        return result;
    }

    private async Task<SeekControlResult> SeekToFromIpcAsync(double positionSeconds, CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => SeekToFromIpcOnUiAsync(positionSeconds, cancellationToken));
        return await await operation.Task;
    }

    private async Task<SeekControlResult> SeekToFromIpcOnUiAsync(double positionSeconds, CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var playerResult = await ExecuteUnifiedPlayerControlOnUiAsync(
            IpcConstants.CommandPlayerSeek,
            positionSeconds,
            volumePercent: null,
            muted: null,
            controlPolicy: null,
            cancellationToken);
        var result = ToSeekControlResult(playerResult);
        LogSeekControlResult(result);
        return result;
    }

    private async Task<UnifiedPlayerStateResult> PlayerControlFromIpcAsync(
        string command,
        IpcCommand? request,
        CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => PlayerControlFromIpcOnUiAsync(command, request, cancellationToken));
        return await await operation.Task;
    }

    private async Task<UnifiedPlayerStateResult> PlayerControlFromIpcOnUiAsync(
        string command,
        IpcCommand? request,
        CancellationToken cancellationToken)
    {
        await WaitForWebViewReadyForIpcAsync(cancellationToken);
        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var positionSeconds = ReadOptionalDouble(request?.PositionSeconds);
        var volumePercent = ReadOptionalInt(request?.VolumePercent);
        var muted = ReadOptionalBool(request?.Muted);
        var expirationSeconds = ReadOptionalInt(request?.ExpirationSeconds) ?? 30;
        UnifiedPlayerControlPolicy? policy = null;
        if (command == IpcConstants.CommandPlayerSetControlPolicy)
        {
            policy = new UnifiedPlayerControlPolicy
            {
                Enabled = request?.Enabled ?? request?.Value ?? true,
                DesiredVolumePercent = volumePercent ?? _desiredVolumePercent,
                DesiredMutedState = muted ?? _desiredMutedState,
                ExpirationSeconds = expirationSeconds
            };
        }

        var result = await ExecuteUnifiedPlayerControlOnUiAsync(
            command,
            positionSeconds,
            volumePercent,
            muted,
            policy,
            cancellationToken);
        LogUnifiedPlayerControlResult(command, result);
        return result;
    }

    private async Task<UnifiedPlayerStateResult> ExecuteUnifiedPlayerControlOnUiAsync(
        string command,
        double? positionSeconds,
        int? volumePercent,
        bool? muted,
        UnifiedPlayerControlPolicy? controlPolicy,
        CancellationToken cancellationToken)
    {
        if (command is IpcConstants.CommandPlay or IpcConstants.CommandPlayerPlay)
        {
            _userPlayCommandReceived = true;
        }

        var operation = ToUnifiedOperation(command);
        var result = await UnifiedPlayerControlService.ExecuteAsync(
            PlayerWebView.CoreWebView2!,
            operation,
            _desiredVolumePercent,
            _desiredMutedState,
            positionSeconds,
            volumePercent,
            muted,
            controlPolicy,
            cancellationToken);

        UpdateDesiredStateFromUnifiedResult(command, result);
        return result;
    }

    private void UpdateDesiredStateFromUnifiedResult(string command, UnifiedPlayerStateResult result)
    {
        if (command == IpcConstants.CommandPlayerClearControlPolicy)
        {
            _desiredVolumePercent = null;
            _desiredMutedState = null;
            return;
        }

        if (command == IpcConstants.CommandPlayerSetControlPolicy
            || (result.ControlPolicyEnabled && command == IpcConstants.CommandPlayerGetState))
        {
            return;
        }

        if (result.DesiredVolumePercent.HasValue)
        {
            _desiredVolumePercent = result.DesiredVolumePercent;
        }

        if (result.DesiredMutedState.HasValue)
        {
            _desiredMutedState = result.DesiredMutedState;
        }
    }

    private static string ToUnifiedOperation(string command)
    {
        return command switch
        {
            IpcConstants.CommandPlay or IpcConstants.CommandPlayerPlay => UnifiedPlayerControlService.OperationPlay,
            IpcConstants.CommandPause or IpcConstants.CommandPlayerPause => UnifiedPlayerControlService.OperationPause,
            IpcConstants.CommandPlayerStop => UnifiedPlayerControlService.OperationStop,
            IpcConstants.CommandSeekTo or IpcConstants.CommandPlayerSeek => UnifiedPlayerControlService.OperationSeek,
            IpcConstants.CommandSeekToStart => UnifiedPlayerControlService.OperationSeekToStart,
            IpcConstants.CommandToggleMute => UnifiedPlayerControlService.OperationToggleMute,
            IpcConstants.CommandSetVolume or IpcConstants.CommandPlayerSetVolume => UnifiedPlayerControlService.OperationSetVolume,
            IpcConstants.CommandSetMuted or IpcConstants.CommandPlayerSetMuted => UnifiedPlayerControlService.OperationSetMuted,
            IpcConstants.CommandPlayerNext => UnifiedPlayerControlService.OperationNext,
            IpcConstants.CommandPlayerPrevious => UnifiedPlayerControlService.OperationPrevious,
            IpcConstants.CommandPlayerNextChapter => UnifiedPlayerControlService.OperationNextChapter,
            IpcConstants.CommandPlayerPreviousChapter => UnifiedPlayerControlService.OperationPreviousChapter,
            IpcConstants.CommandPlayerSetControlPolicy => UnifiedPlayerControlService.OperationSetControlPolicy,
            IpcConstants.CommandPlayerClearControlPolicy => UnifiedPlayerControlService.OperationClearControlPolicy,
            UnifiedPlayerControlService.OperationReapplyDesiredState => UnifiedPlayerControlService.OperationReapplyDesiredState,
            _ => UnifiedPlayerControlService.OperationGetState
        };
    }

    private static MediaControlResult ToMediaControlResult(UnifiedPlayerStateResult result)
    {
        return new MediaControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            DocumentReadyState = string.Empty,
            VideoElementCount = string.Equals(result.TargetElementTag, "video", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            AudioElementCount = string.Equals(result.TargetElementTag, "audio", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            OperationError = result.OperationError,
            IsPaused = result.IsPaused,
            BeforeMuted = null,
            AfterMuted = result.IsMuted,
            IsMuted = result.IsMuted,
            CurrentTime = result.CurrentTimeSeconds,
            Duration = result.DurationSeconds,
            DurationMs = result.DurationMs
        };
    }

    private static AudioControlResult ToAudioControlResult(UnifiedPlayerStateResult result)
    {
        return new AudioControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = result.CurrentUrl,
            MediaTitle = result.Title,
            Volume = result.VolumePercent.HasValue ? result.VolumePercent.Value / 100.0 : null,
            VolumePercent = result.VolumePercent,
            RequestedVolumePercent = result.RequestedVolumePercent,
            AppliedVolume = result.VolumePercent.HasValue ? result.VolumePercent.Value / 100.0 : null,
            AppliedVolumePercent = result.VolumePercent,
            DesiredVolumePercent = result.DesiredVolumePercent,
            IsMuted = result.IsMuted,
            RequestedMuted = result.RequestedMuted,
            ActualMuted = result.IsMuted,
            MutePersistenceEnabled = result.DesiredMutedState == true,
            DesiredMutedState = result.DesiredMutedState,
            MediaElementCount = result.MediaElementCount,
            VideoElementCount = string.Equals(result.TargetElementTag, "video", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            AudioElementCount = string.Equals(result.TargetElementTag, "audio", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            IsPlaying = result.IsPlaying,
            IsPaused = result.IsPaused,
            CurrentTime = result.CurrentTimeSeconds,
            CurrentTimeSeconds = result.CurrentTimeSeconds,
            Duration = result.DurationSeconds,
            DurationSeconds = result.DurationSeconds,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            OperationError = result.OperationError,
            DurationMs = result.DurationMs
        };
    }

    private static SeekControlResult ToSeekControlResult(UnifiedPlayerStateResult result)
    {
        return new SeekControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            RequestedPositionSeconds = result.RequestedPositionSeconds,
            ActualPositionSeconds = result.CurrentTimeSeconds,
            Duration = result.DurationSeconds,
            DurationSeconds = result.DurationSeconds,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            CurrentUrl = result.CurrentUrl,
            MediaTitle = result.Title,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            MediaElementCount = result.MediaElementCount,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            OperationError = result.OperationError,
            DurationMs = result.DurationMs
        };
    }

    private static int? ReadOptionalInt(JsonElement? value)
    {
        return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static double? ReadOptionalDouble(JsonElement? value)
    {
        return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var result)
            ? result
            : null;
    }

    private static bool? ReadOptionalBool(JsonElement? value)
    {
        return value.HasValue
            ? value.Value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }
            : null;
    }

    private static AudioControlResult WithDesiredAudioState(
        AudioControlResult result,
        int? desiredVolumePercent,
        bool? desiredMutedState)
    {
        return new AudioControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = result.CurrentUrl,
            MediaTitle = result.MediaTitle,
            Volume = result.Volume,
            VolumePercent = result.VolumePercent,
            RequestedVolumePercent = result.RequestedVolumePercent,
            AppliedVolume = result.AppliedVolume,
            AppliedVolumePercent = result.AppliedVolumePercent,
            IsMuted = result.IsMuted,
            RequestedMuted = result.RequestedMuted,
            ActualMuted = result.ActualMuted,
            DesiredVolumePercent = desiredVolumePercent,
            MutePersistenceEnabled = result.MutePersistenceEnabled,
            DesiredMutedState = desiredMutedState,
            MediaElementCount = result.MediaElementCount,
            VideoElementCount = result.VideoElementCount,
            AudioElementCount = result.AudioElementCount,
            IsPlaying = result.IsPlaying,
            IsPaused = result.IsPaused,
            CurrentTime = result.CurrentTime,
            CurrentTimeSeconds = result.CurrentTimeSeconds,
            Duration = result.Duration,
            DurationSeconds = result.DurationSeconds,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            PlaybackRate = result.PlaybackRate,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            OperationError = result.OperationError,
            DurationMs = result.DurationMs
        };
    }

    private async Task<InlineFullscreenResult> ControlInlineFullscreenFromIpcAsync(
        string command,
        CancellationToken cancellationToken)
    {
        var operation = Dispatcher.InvokeAsync(() => ControlInlineFullscreenFromIpcOnUiAsync(command, cancellationToken));
        return await await operation.Task;
    }

    private async Task<InlineFullscreenResult> ControlInlineFullscreenFromIpcOnUiAsync(
        string command,
        CancellationToken cancellationToken)
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
            Debug.WriteLine($"IPC inline fullscreen waited for WebView2 readiness and failed: {ex.Message}");
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage, ex);
        }

        if (PlayerWebView.CoreWebView2 is null)
        {
            throw new InvalidOperationException(IpcConstants.WebViewNotReadyMessage);
        }

        var result = command switch
        {
            IpcConstants.CommandEnterInlineFullscreen => await InlineFullscreenService.EnterAsync(
                PlayerWebView.CoreWebView2,
                cancellationToken),
            IpcConstants.CommandExitInlineFullscreen => await InlineFullscreenService.ExitAsync(
                PlayerWebView.CoreWebView2,
                cancellationToken),
            IpcConstants.CommandToggleInlineFullscreen => await InlineFullscreenService.ToggleAsync(
                PlayerWebView.CoreWebView2,
                cancellationToken),
            _ => new InlineFullscreenResult
            {
                Success = false,
                ErrorCode = IpcConstants.ErrorCodeUnsupportedCommand,
                Message = IpcConstants.UnsupportedCommandMessage,
                CurrentUrl = GetCurrentWebViewUrl()
            }
        };

        result = await RefreshInlineFullscreenResultStateAsync(result, cancellationToken);
        LogInlineFullscreenResult(command, result);
        return result;
    }

    private async Task<InlineFullscreenResult> RefreshInlineFullscreenResultStateAsync(
        InlineFullscreenResult result,
        CancellationToken cancellationToken)
    {
        if (PlayerWebView.CoreWebView2 is null || !result.YoutubeDetected || !result.Success)
        {
            return result;
        }

        await Task.Delay(350, cancellationToken);
        var state = await InlineFullscreenService.InspectAsync(PlayerWebView.CoreWebView2, cancellationToken);
        return new InlineFullscreenResult
        {
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = state.CurrentUrl ?? result.CurrentUrl,
            YoutubeDetected = state.YoutubeDetected,
            IsShorts = state.IsShorts,
            InlineFullscreenBefore = result.InlineFullscreenBefore,
            InlineFullscreenAfter = state.IsInlineFullscreen,
            IsInlineFullscreen = state.IsInlineFullscreen,
            DomOperationResult = result.DomOperationResult,
            FullscreenApiResult = result.FullscreenApiResult,
            OperationError = result.OperationError,
            DurationMs = result.DurationMs + state.DurationMs
        };
    }

    private void LogInlineFullscreenResult(string command, InlineFullscreenResult result)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=InlineFullscreenCommand"
            + "; PID=" + Environment.ProcessId
            + "; Pipe=" + _ipcPipeName
            + "; Command=" + command
            + "; Url=" + DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl())
            + "; WebViewReady=" + _webViewReadyCompletion.Task.IsCompletedSuccessfully
            + "; CoreWebView2Ready=" + (PlayerWebView.CoreWebView2 is not null)
            + "; YouTubeDetected=" + result.YoutubeDetected
            + "; InlineFullscreenBefore=" + FormatNullable(result.InlineFullscreenBefore)
            + "; InlineFullscreenAfter=" + FormatNullable(result.InlineFullscreenAfter)
            + "; DomOperationResult=" + (result.DomOperationResult ?? string.Empty)
            + "; FullscreenApiResult=" + (result.FullscreenApiResult ?? string.Empty)
            + "; Result=" + (result.Success ? "Success" : "Failed")
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; Message=" + (result.Message ?? string.Empty)
            + "; OperationError=" + (result.OperationError ?? string.Empty)
            + "; DurationMs=" + result.DurationMs);
    }

    private void LogMediaControlAttemptResult(string command, MediaControlResult result)
    {
        LogMediaControlResult(command, result, "MediaControlAttempt");
    }

    private void LogMediaControlResult(string command, MediaControlResult result)
    {
        LogMediaControlResult(command, result, "MediaControlResult");
    }

    private void LogMediaControlResult(string command, MediaControlResult result, string eventName)
    {
        var currentUrl = DiagnosticLogService.FormatUrlForLog(GetCurrentWebViewUrl());
        var resultText = result.Success && result.MediaFound ? "Success" : "Failed";
        var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode) ? string.Empty : result.ErrorCode;
        DiagnosticLogService.Write(
            "IPC",
            "Event=" + eventName
            + "; PID=" + Environment.ProcessId
            + "; Pipe=" + _ipcPipeName
            + "; Command=" + command
            + "; Url=" + currentUrl
            + "; WebViewReady=" + _webViewReadyCompletion.Task.IsCompletedSuccessfully
            + "; CoreWebView2Ready=" + (PlayerWebView.CoreWebView2 is not null)
            + "; NavigationCompleted=" + _navigationCompleted
            + "; ReadyState=" + (result.DocumentReadyState ?? string.Empty)
            + "; VideoCount=" + result.VideoElementCount
            + "; AudioCount=" + result.AudioElementCount
            + "; IframeCount=" + result.IframeElementCount
            + "; Target=" + (result.TargetElementTag ?? string.Empty)
            + "; Result=" + resultText
            + "; ErrorCode=" + errorCode
            + "; AttemptCount=" + result.AttemptCount
            + "; Paused=" + FormatNullable(result.IsPaused)
            + "; Muted=" + FormatNullable(result.IsMuted)
            + "; CurrentTime=" + FormatNullable(result.CurrentTime)
            + "; BeforeMuted=" + FormatNullable(result.BeforeMuted)
            + "; AfterMuted=" + FormatNullable(result.AfterMuted)
            + "; BeforeCurrentTime=" + FormatNullable(result.BeforeCurrentTime)
            + "; AfterCurrentTime=" + FormatNullable(result.AfterCurrentTime)
            + "; MediaReadyState=" + (result.ReadyState?.ToString() ?? string.Empty)
            + "; NetworkState=" + (result.NetworkState?.ToString() ?? string.Empty)
            + "; DisplayWidth=" + FormatNullable(result.DisplayWidth)
            + "; DisplayHeight=" + FormatNullable(result.DisplayHeight)
            + "; Display=" + (result.Display ?? string.Empty)
            + "; Visibility=" + (result.Visibility ?? string.Empty)
            + "; OperationError=" + (result.OperationError ?? string.Empty)
            + "; DurationMs=" + result.DurationMs);
    }

    private void LogAudioControlResult(string command, AudioControlResult result, bool detailed)
    {
        var currentUrl = DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl());
        var message = "Event=AudioControl"
            + "; PID=" + Environment.ProcessId
            + "; PipeName=" + _ipcPipeName
            + "; Command=" + command
            + "; RequestedVolume=" + (result.RequestedVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; DesiredVolume=" + (result.DesiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? _desiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; AppliedVolume=" + (result.AppliedVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? result.VolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; RequestedMuted=" + FormatNullable(result.RequestedMuted)
            + "; DesiredMuted=" + FormatNullable(result.DesiredMutedState ?? _desiredMutedState)
            + "; ActualMuted=" + FormatNullable(result.ActualMuted ?? result.IsMuted)
            + "; MediaIdentity=" + (result.MediaIdentity ?? string.Empty)
            + "; MediaRevision=" + result.MediaRevision
            + "; MediaElementCount=" + result.MediaElementCount
            + "; CurrentUrl=" + currentUrl
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; Duration=" + result.DurationMs;

        if (detailed)
        {
            message += "; Result=" + (result.Success && result.MediaFound ? "Success" : "Failed")
                + "; Volume=" + FormatNullable(result.Volume)
                + "; IsPlaying=" + FormatNullable(result.IsPlaying)
                + "; CurrentTime=" + FormatNullable(result.CurrentTime)
                + "; MediaTitle=" + (result.MediaTitle ?? string.Empty)
                + "; OperationError=" + (result.OperationError ?? string.Empty);
        }

        DiagnosticLogService.Write("IPC", message);
    }

    private void LogUnifiedPlayerControlResult(string command, UnifiedPlayerStateResult result)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=UnifiedPlayerControl"
            + "; PID=" + Environment.ProcessId
            + "; PipeName=" + _ipcPipeName
            + "; Command=" + command
            + "; Operation=" + (result.Operation ?? string.Empty)
            + "; OperationResult=" + (result.OperationResult ?? string.Empty)
            + "; SiteType=" + result.SiteType
            + "; PlayerType=" + result.PlayerType
            + "; MediaIdentity=" + (result.MediaIdentity ?? string.Empty)
            + "; MediaRevision=" + result.MediaRevision
            + "; RequestedVolume=" + (result.RequestedVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; DesiredVolume=" + (result.DesiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; ActualVolume=" + (result.VolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; RequestedMuted=" + FormatNullable(result.RequestedMuted)
            + "; DesiredMuted=" + FormatNullable(result.DesiredMutedState)
            + "; ActualMuted=" + FormatNullable(result.IsMuted)
            + "; IsPlaying=" + FormatNullable(result.IsPlaying)
            + "; IsEnded=" + FormatNullable(result.IsEnded)
            + "; EndedReason=" + (result.EndedReason ?? string.Empty)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl())
            + "; CurrentSrc=" + DiagnosticLogService.FormatUrlForLog(result.CurrentSrc ?? string.Empty)
            + "; Result=" + (result.Success && result.MediaFound ? "Success" : "Failed")
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; OperationError=" + (result.OperationError ?? string.Empty)
            + "; DurationMs=" + result.DurationMs);
    }

    private async Task<AudioPersistenceResult> ApplyAudioPersistenceOnUiAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return new AudioPersistenceResult
            {
                Success = false,
                ErrorCode = IpcConstants.ErrorCodeWebViewNotReady,
                Message = IpcConstants.WebViewNotReadyMessage,
                CurrentUrl = GetCurrentWebViewUrl(),
                DesiredVolumePercent = _desiredVolumePercent,
                DesiredMutedState = _desiredMutedState
            };
        }

        var result = await AudioPersistenceService.SetAsync(
            PlayerWebView.CoreWebView2,
            _desiredVolumePercent,
            _desiredMutedState,
            reason,
            cancellationToken);
        UpdateAudioPersistenceState(result);
        return result;
    }

    private void UpdateAudioPersistenceState(AudioPersistenceResult result)
    {
        if (result.DesiredVolumePercent.HasValue)
        {
            _desiredVolumePercent = result.DesiredVolumePercent;
        }

        if (result.DesiredMutedState.HasValue)
        {
            _desiredMutedState = result.DesiredMutedState;
        }
    }

    private void LogAudioPersistenceResult(string command, AudioPersistenceResult result)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=AudioPersistence"
            + "; PID=" + Environment.ProcessId
            + "; PipeName=" + _ipcPipeName
            + "; Command=" + command
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl())
            + "; MediaIdentity=" + (result.MediaIdentity ?? string.Empty)
            + "; MediaRevision=" + result.MediaRevision
            + "; DesiredVolume=" + (result.DesiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; ActualVolume=" + (result.ActualVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            + "; DesiredMuted=" + FormatNullable(result.DesiredMutedState)
            + "; ActualMuted=" + FormatNullable(result.ActualMutedState)
            + "; MediaElementCount=" + result.MediaElementCount
            + "; MediaElementChanged=" + result.MediaElementChanged
            + "; TargetElementTag=" + (result.TargetElementTag ?? string.Empty)
            + "; CurrentSrc=" + DiagnosticLogService.FormatUrlForLog(result.CurrentSrc ?? string.Empty)
            + "; ReadyState=" + (result.ReadyState?.ToString() ?? string.Empty)
            + "; ReapplyReason=" + (result.ReapplyReason ?? string.Empty)
            + "; ReapplyResult=" + (result.ReapplyResult ?? string.Empty)
            + "; Result=" + (result.Success ? "Success" : "Failed")
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; OperationError=" + (result.OperationError ?? string.Empty)
            + "; DurationMs=" + result.DurationMs);
    }

    private void LogSeekControlResult(SeekControlResult result)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=SeekControl"
            + "; PID=" + Environment.ProcessId
            + "; PipeName=" + _ipcPipeName
            + "; Command=" + IpcConstants.CommandSeekTo
            + "; RequestedPositionSeconds=" + FormatNullable(result.RequestedPositionSeconds)
            + "; ActualPositionSeconds=" + FormatNullable(result.ActualPositionSeconds)
            + "; Duration=" + FormatNullable(result.DurationSeconds ?? result.Duration)
            + "; IsSeekable=" + result.IsSeekable
            + "; IsLive=" + result.IsLive
            + "; MediaIdentity=" + (result.MediaIdentity ?? string.Empty)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl())
            + "; Result=" + (result.Success && result.MediaFound ? "Success" : "Failed")
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; DurationMs=" + result.DurationMs);
    }

    private async Task<MutePersistenceResult> ApplyMutePersistenceOnUiAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return new MutePersistenceResult
            {
                Success = false,
                ErrorCode = IpcConstants.ErrorCodeWebViewNotReady,
                Message = IpcConstants.WebViewNotReadyMessage,
                CurrentUrl = GetCurrentWebViewUrl(),
                MutePersistenceEnabled = _mutePersistenceEnabled,
                DesiredMutedState = _desiredMutedState
            };
        }

        var result = await MutePersistenceService.SetAsync(
            PlayerWebView.CoreWebView2,
            _mutePersistenceEnabled,
            _desiredMutedState,
            reason,
            cancellationToken);
        UpdateMutePersistenceState(result);
        return result;
    }

    private void UpdateMutePersistenceState(MutePersistenceResult result)
    {
        _mutePersistenceEnabled = result.MutePersistenceEnabled;
        if (result.DesiredMutedState.HasValue)
        {
            _desiredMutedState = result.DesiredMutedState;
        }

        if (!string.IsNullOrWhiteSpace(result.LastMuteReapplyReason))
        {
            _lastMuteReapplyReason = result.LastMuteReapplyReason;
        }
    }

    private void LogMutePersistenceResult(string command, MutePersistenceResult result)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=MutePersistence"
            + "; PID=" + Environment.ProcessId
            + "; PipeName=" + _ipcPipeName
            + "; Command=" + command
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(result.CurrentUrl ?? GetCurrentWebViewUrl())
            + "; MutePersistenceEnabled=" + result.MutePersistenceEnabled
            + "; DesiredMutedState=" + FormatNullable(result.DesiredMutedState)
            + "; ActualMutedStateBefore=" + FormatNullable(result.ActualMutedStateBefore)
            + "; ActualMutedStateAfter=" + FormatNullable(result.ActualMutedStateAfter)
            + "; ActualMutedState=" + FormatNullable(result.ActualMutedState)
            + "; MediaElementCount=" + result.MediaElementCount
            + "; MediaElementChanged=" + result.MediaElementChanged
            + "; ReapplyReason=" + (result.ReapplyReason ?? string.Empty)
            + "; ReapplyResult=" + (result.ReapplyResult ?? string.Empty)
            + "; Result=" + (result.Success ? "Success" : "Failed")
            + "; ErrorCode=" + (result.ErrorCode ?? string.Empty)
            + "; OperationError=" + (result.OperationError ?? string.Empty)
            + "; Duration=" + result.DurationMs);
    }

    private static string FormatNullable(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : string.Empty;
    }

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    private async Task<IpcStatusData> GetIpcStatusAsync(CancellationToken cancellationToken)
    {
        if (!Dispatcher.CheckAccess())
        {
            var operation = Dispatcher.InvokeAsync(() => GetIpcStatusOnUiAsync(cancellationToken));
            return await await operation.Task;
        }

        return await GetIpcStatusOnUiAsync(cancellationToken);
    }

    private async Task<IpcStatusData> GetIpcStatusOnUiAsync(CancellationToken cancellationToken)
    {
        InlineFullscreenResult? inlineFullscreen = null;
        MutePersistenceResult? mutePersistence = null;
        if (PlayerWebView.CoreWebView2 is not null)
        {
            inlineFullscreen = await InlineFullscreenService.InspectAsync(
                PlayerWebView.CoreWebView2,
                cancellationToken);
            var playerState = await ExecuteUnifiedPlayerControlOnUiAsync(
                IpcConstants.CommandPlayerGetState,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);
            LogUnifiedPlayerControlResult(IpcConstants.CommandPlayerGetState, playerState);
            mutePersistence = await MutePersistenceService.InspectAsync(
                PlayerWebView.CoreWebView2,
                _mutePersistenceEnabled,
                _desiredMutedState,
                cancellationToken);
            UpdateMutePersistenceState(mutePersistence);
        }

        var mediaElementCount = Math.Max(
            (_lastMediaControlResult?.VideoElementCount ?? 0)
            + (_lastMediaControlResult?.AudioElementCount ?? 0),
            mutePersistence?.MediaElementCount ?? 0);

        return new IpcStatusData
        {
            ProcessId = Environment.ProcessId,
            WindowTitle = Title ?? string.Empty,
            IsPlayerMode = _isPlayerMode,
            IsIpcEnabled = _startupOptions.IsIpcEnabled,
            IpcEnabled = _startupOptions.IsIpcEnabled,
            PipeServerStarted = _pipeServerStarted,
            PipeName = _ipcPipeName,
            LastIpcError = _lastIpcError,
            MediaElementCount = mediaElementCount,
            VideoElementCount = _lastMediaControlResult?.VideoElementCount ?? 0,
            AudioElementCount = _lastMediaControlResult?.AudioElementCount ?? 0,
            DocumentReadyState = _lastMediaControlResult?.DocumentReadyState ?? string.Empty,
            LastMediaCommand = _lastMediaCommand,
            LastMediaCommandResult = _lastMediaControlResult is null
                ? string.Empty
                : (_lastMediaControlResult.Success && _lastMediaControlResult.MediaFound ? "Success" : "Failed"),
            LastMediaErrorCode = _lastMediaControlResult?.ErrorCode ?? string.Empty,
            CurrentUrl = GetCurrentWebViewUrl() ?? string.Empty,
            IsWebViewReady = PlayerWebView.CoreWebView2 is not null,
            IsInlineFullscreen = inlineFullscreen?.IsInlineFullscreen == true,
            InlineFullscreenStateKnown = inlineFullscreen?.Success == true,
            InlineFullscreenErrorCode = inlineFullscreen?.ErrorCode ?? string.Empty,
            MutePersistenceEnabled = _mutePersistenceEnabled,
            DesiredVolumePercent = _desiredVolumePercent,
            DesiredMutedState = mutePersistence?.DesiredMutedState ?? _desiredMutedState,
            ActualMutedState = mutePersistence?.ActualMutedState,
            LastMuteReapplyReason = mutePersistence?.LastMuteReapplyReason ?? _lastMuteReapplyReason,
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
        _navigationCompleted = false;
        if (_pendingResizeSnapshot is not null || _isFullScreenTransitioning)
        {
            _navigationStartedDuringWindowTransition = true;
        }
        DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("NavigationStarting", null, null));
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
            || uri.Host.Equals("music.youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    private void PlayerWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navigationCompleted = true;
        if (_pendingResizeSnapshot is not null || _isFullScreenTransitioning)
        {
            _navigationCompletedDuringWindowTransition = true;
        }
        DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("NavigationCompleted", null, null));
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

        if (e.IsSuccess)
        {
            ScheduleInitialStartPaused();
            if (_pendingFavoritePlaybackRequest is not null)
            {
                _ = ApplyPendingFavoritePlaybackAsync(_pendingFavoritePlaybackRequest);
            }

            if (_desiredVolumePercent.HasValue || _desiredMutedState.HasValue)
            {
                _ = ApplyUnifiedPlayerControlAfterNavigationAsync();
            }

            if (_mutePersistenceEnabled)
            {
                _ = ApplyMutePersistenceAfterNavigationAsync();
            }
        }
        else
        {
            CancelPendingFavoritePlayback();
        }
    }

    private async Task ApplyUnifiedPlayerControlAfterNavigationAsync()
    {
        try
        {
            UnifiedPlayerStateResult? lastResult = null;
            for (var attempt = 1; attempt <= 8; attempt++)
            {
                await Task.Delay(attempt == 1 ? 350 : 250);
                lastResult = await ExecuteUnifiedPlayerControlOnUiAsync(
                    UnifiedPlayerControlService.OperationReapplyDesiredState,
                    positionSeconds: null,
                    volumePercent: null,
                    muted: null,
                    controlPolicy: null,
                    CancellationToken.None);
                LogUnifiedPlayerControlResult($"navigation-reapply-{attempt}", lastResult);

                if (lastResult.Success
                    || string.Equals(lastResult.OperationResult, "no-desired-state", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            if (lastResult is not null)
            {
                DiagnosticLogService.Write(
                    "IPC",
                    "Event=UnifiedPlayerNavigationReapplyFailed"
                    + "; DesiredVolume=" + (_desiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                    + "; DesiredMuted=" + FormatNullable(_desiredMutedState)
                    + "; ActualVolume=" + (lastResult.VolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                    + "; ActualMuted=" + FormatNullable(lastResult.IsMuted)
                    + "; MediaIdentity=" + (lastResult.MediaIdentity ?? string.Empty)
                    + "; MediaRevision=" + lastResult.MediaRevision
                    + "; RetryCount=8"
                    + "; Result=Failed"
                    + "; ErrorCode=" + (lastResult.ErrorCode ?? string.Empty));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("IPC", "Unified player navigation apply failed.", ex);
        }
    }

    private async Task ApplyMutePersistenceAfterNavigationAsync()
    {
        try
        {
            await Task.Delay(350);
            var result = await ApplyMutePersistenceOnUiAsync("navigation-completed", CancellationToken.None);
            LogMutePersistenceResult("navigation-completed", result);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("IPC", "Mute persistence navigation apply failed.", ex);
        }
    }

    private void ScheduleInitialStartPaused()
    {
        if (!_startupOptions.StartPaused
            || _initialStartPausedScheduled
            || _initialStartPausedCompleted)
        {
            return;
        }

        _initialStartPausedScheduled = true;
        DiagnosticLogService.Write("Startup", $"InitialStartPaused scheduled. ProcessId: {Environment.ProcessId}");
        _ = RunInitialStartPausedAsync();
    }

    private async Task RunInitialStartPausedAsync()
    {
        for (var attempt = 1; attempt <= InitialStartPausedMaxAttempts; attempt++)
        {
            if (_userPlayCommandReceived)
            {
                DiagnosticLogService.Write("Startup", $"InitialStartPaused stopped because play was requested. ProcessId: {Environment.ProcessId}");
                break;
            }

            try
            {
                if (attempt > 1)
                {
                    await Task.Delay(InitialStartPausedRetryDelayMilliseconds);
                }

                if (PlayerWebView.CoreWebView2 is null)
                {
                    DiagnosticLogService.Write("Startup", $"InitialStartPaused attempt {attempt}: WebView2 is not ready. ProcessId: {Environment.ProcessId}");
                    continue;
                }

                var result = await MediaControlService.ExecuteAsync(
                    PlayerWebView.CoreWebView2,
                    IpcConstants.CommandPause,
                    CancellationToken.None);

                DiagnosticLogService.Write(
                    "Startup",
                    $"InitialStartPaused attempt {attempt}: ProcessId={Environment.ProcessId}, Success={result.Success}, MediaFound={result.MediaFound}, IsPaused={result.IsPaused}");

                if (result.Success)
                {
                    _initialStartPausedCompleted = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogService.WriteException(
                    "Startup",
                    $"InitialStartPaused attempt {attempt} failed. ProcessId: {Environment.ProcessId}",
                    ex);
            }
        }

        if (!_initialStartPausedCompleted && !_userPlayCommandReceived)
        {
            DiagnosticLogService.Write("Startup", $"InitialStartPaused gave up. ProcessId: {Environment.ProcessId}");
        }
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
            button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Stretch;
            button.Padding = new Thickness(0);
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
        var label = CreateBookmarkLabelTextBlock(bookmark);
        var backgroundBrush = TryCreateBookmarkBackgroundBrush(bookmark);
        if (backgroundBrush is null)
        {
            return label;
        }

        var panel = new Grid
        {
            Background = backgroundBrush,
            ClipToBounds = true,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        System.Windows.Controls.Panel.SetZIndex(label, 1);
        panel.Children.Add(label);

        return panel;
    }

    private static TextBlock CreateBookmarkLabelTextBlock(BookmarkItem bookmark)
    {
        return new TextBlock
        {
            Text = CreateBookmarkDisplayLabel(bookmark.Label),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = bookmark.IsBold ? FontWeights.Bold : FontWeights.Normal,
            MaxWidth = 96,
            Padding = new Thickness(2)
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

    private static ImageBrush? TryCreateBookmarkBackgroundBrush(BookmarkItem bookmark)
    {
        if (!TryResolveBookmarkImagePath(bookmark.IconPath, out var fullPath) || !File.Exists(fullPath))
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
            using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = AppConstants.FavoriteBackgroundImageDecodePixelSize;
            bitmap.EndInit();
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap)
            {
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.UniformToFill,
                TileMode = TileMode.None
            };
            brush.Freeze();
            return brush;
        }
        catch (Exception ex)
        {
            DiagnosticLogService.Write(
                "Favorite",
                "Event=FavoriteBackgroundImageLoadFailed"
                + "; Path=" + fullPath
                + "; ExceptionType=" + ex.GetType().Name
                + "; Message=" + ex.Message);
            return null;
        }
    }

    private static bool TryResolveBookmarkImagePath(string? iconPath, out string fullPath)
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
        if (sender is WpfButton { Tag: BookmarkItem bookmark } button)
        {
            var slotNumber = Array.IndexOf(_favoriteButtons, button) + 1;
            NavigateBookmark(bookmark, slotNumber > 0 ? slotNumber : bookmark.SortOrder);
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

    private void ResetFavoriteToHomeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: int index })
        {
            return;
        }

        ResetFavoriteToHome(index);
    }

    private void ResetFavoriteToHome(int index)
    {
        var previous = _bookmarks.ElementAtOrDefault(index);
        var homeUrl = GetCurrentValidHomeUrl();
        var confirmed = false;
        try
        {
            var result = System.Windows.MessageBox.Show(
                AppConstants.FavoriteResetToHomeConfirmMessage,
                AppConstants.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            confirmed = result == MessageBoxResult.Yes;
            if (!confirmed)
            {
                LogFavoriteResetContextMenu(index, shiftPressed: true, "ResetCancelled", confirmed, saveResult: "Skipped", result: "Cancelled", errorCode: string.Empty);
                return;
            }

            var bookmarks = _bookmarks.ToList();
            while (bookmarks.Count < AppConstants.MaxBookmarks)
            {
                bookmarks.Add(new BookmarkItem { SortOrder = bookmarks.Count + 1 });
            }

            bookmarks[index] = CreateHomeBookmarkForSlot(index + 1, homeUrl);
            _bookmarkService.Save(bookmarks);
            _bookmarks = _bookmarkService.Load();
            ApplyBookmarks(_bookmarks);
            if (_openSettingsWindow is { IsLoaded: true } settingsWindow)
            {
                settingsWindow.ReloadBookmarksFromFile();
            }

            LoadingStatusText.Text = $"状態: お気に入り{index + 1:00}をHomeへ初期化しました";
            LogFavoriteResetContextMenu(index, shiftPressed: true, "ResetApplied", confirmed, saveResult: "Saved", result: "Success", errorCode: string.Empty, previous, homeUrl);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("Favorite", "Favorite reset to Home failed.", ex);
            LoadingStatusText.Text = AppConstants.FavoriteResetToHomeFailedMessage;
            System.Windows.MessageBox.Show(
                AppConstants.FavoriteResetToHomeFailedMessage,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            LogFavoriteResetContextMenu(index, shiftPressed: true, "ResetFailed", confirmed, saveResult: "Failed", result: "Failed", errorCode: ex.GetType().Name, previous, homeUrl);
        }
    }

    private BookmarkItem CreateHomeBookmarkForSlot(int sortOrder, string homeUrl)
    {
        return new BookmarkItem
        {
            Label = AppConstants.DefaultHomeBookmarkLabel,
            Url = homeUrl,
            SortOrder = sortOrder,
            IsEnabled = true,
            BackgroundColor = AppConstants.DefaultBookmarkBackgroundColor,
            ForegroundColor = AppConstants.DefaultBookmarkForegroundColor,
            IsBold = false,
            IconPath = string.Empty,
            Autoplay = false,
            Mute = false,
            Loop = false,
            StartPositionSeconds = 0
        };
    }

    private string GetCurrentValidHomeUrl()
    {
        var loaded = _settingsService.Load();
        var candidate = loaded.HomeUrl;
        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : AppConstants.DefaultHomeUrl;
    }

    private bool IsFavoriteRegisteredForReset(int index)
    {
        var bookmark = _bookmarks.ElementAtOrDefault(index);
        if (bookmark is null)
        {
            return false;
        }

        return !IsHomeInitialBookmark(bookmark, GetCurrentValidHomeUrl());
    }

    private static bool IsHomeInitialBookmark(BookmarkItem bookmark, string homeUrl)
    {
        return bookmark.IsEnabled
            && string.Equals(bookmark.Label?.Trim(), AppConstants.DefaultHomeBookmarkLabel, StringComparison.Ordinal)
            && AreSameAbsoluteUrl(bookmark.Url, homeUrl)
            && string.IsNullOrWhiteSpace(bookmark.IconPath)
            && string.Equals(bookmark.BackgroundColor, AppConstants.DefaultBookmarkBackgroundColor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(bookmark.ForegroundColor, AppConstants.DefaultBookmarkForegroundColor, StringComparison.OrdinalIgnoreCase)
            && !bookmark.IsBold
            && bookmark.StartPositionSeconds == 0
            && !bookmark.Autoplay
            && !bookmark.Mute
            && !bookmark.Loop;
    }

    private void LogFavoriteResetContextMenu(
        int index,
        bool shiftPressed,
        string eventName,
        bool? confirmed,
        string saveResult,
        string result,
        string errorCode,
        BookmarkItem? previous = null,
        string? homeUrl = null)
    {
        previous ??= _bookmarks.ElementAtOrDefault(index);
        homeUrl ??= GetCurrentValidHomeUrl();
        DiagnosticLogService.Write(
            "Favorite",
            "Event=" + eventName
            + "; Slot=" + (index + 1)
            + "; ShiftPressed=" + shiftPressed
            + "; PreviousDisplayName=" + (previous?.Label ?? string.Empty)
            + "; PreviousUrl=" + DiagnosticLogService.FormatUrlForLog(previous?.Url)
            + "; HomeUrl=" + DiagnosticLogService.FormatUrlForLog(homeUrl)
            + "; Confirmed=" + (confirmed?.ToString() ?? string.Empty)
            + "; SaveResult=" + saveResult
            + "; SyncResult=" + (saveResult == "Saved" ? "FileWatcher" : string.Empty)
            + "; Result=" + result
            + "; ErrorCode=" + errorCode);
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
            NavigateBookmark(bookmark, index + 1);
        }
    }

    private void NavigateBookmark(BookmarkItem bookmark, int slotNumber)
    {
        var navigationUrl = FavoritePlaybackUrlService.GetNavigationUrl(bookmark);
        CancelPendingFavoritePlayback();
        var registeredParseResult = YouTubeUrlService.TryParse(
            bookmark.Url,
            out var videoId,
            out var registeredUrlType,
            out var registeredParseFailureReason);
        var registeredVideoId = registeredParseResult ? videoId : string.Empty;
        var currentUrl = GetCurrentWebViewUrl();
        var currentParseResult = YouTubeUrlService.TryParse(
            currentUrl,
            out var currentId,
            out var currentUrlType,
            out var currentParseFailureReason);
        var currentVideoId = currentParseResult ? currentId : string.Empty;
        var sameUrl = AreSameAbsoluteUrl(currentUrl, navigationUrl);
        var sameVideo = !string.IsNullOrWhiteSpace(registeredVideoId)
            && registeredVideoId.Equals(currentVideoId, StringComparison.Ordinal);
        var navigationSkipped = sameVideo;

        LogYouTubeVideoIdParse(
            bookmark.Url,
            currentUrl,
            registeredVideoId,
            currentVideoId,
            registeredUrlType,
            currentUrlType,
            sameVideo,
            registeredParseResult,
            currentParseResult,
            registeredParseFailureReason,
            currentParseFailureReason);

        _pendingFavoritePlaybackRequest = new FavoritePlaybackRequest(
            Guid.NewGuid(),
            slotNumber,
            bookmark.Url,
            registeredVideoId,
            currentUrl ?? string.Empty,
            currentVideoId,
            sameUrl,
            sameVideo,
            navigationSkipped,
            bookmark.StartPositionSeconds,
            bookmark.Autoplay,
            bookmark.Mute,
            bookmark.Loop);

        _favoritePlaybackCancellation = new CancellationTokenSource();
        LogFavoritePlaybackRequest(_pendingFavoritePlaybackRequest);

        if (navigationSkipped)
        {
            _ = ApplyPendingFavoritePlaybackAsync(_pendingFavoritePlaybackRequest);
            return;
        }

        NavigateTrusted(navigationUrl, preservePendingFavoritePlayback: true);
    }

    private async Task ApplyPendingFavoritePlaybackAsync(FavoritePlaybackRequest request)
    {
        try
        {
            await ApplyPendingFavoritePlaybackCoreAsync(request);
        }
        catch (OperationCanceledException)
        {
            LogFavoritePlaybackSkipped(request, "cancelled", "operation-cancelled");
        }
    }

    private async Task ApplyPendingFavoritePlaybackCoreAsync(FavoritePlaybackRequest request)
    {
        if (!ReferenceEquals(_pendingFavoritePlaybackRequest, request))
        {
            LogFavoritePlaybackSkipped(request, "start", "not-latest-request");
            return;
        }

        var cancellationToken = _favoritePlaybackCancellation?.Token ?? CancellationToken.None;
        FavoritePlaybackWaitResult? lastWaitResult = null;
        for (var attempt = 1; attempt <= FavoritePlaybackMaxAttempts; attempt++)
        {
            try
            {
                await Task.Delay(FavoritePlaybackRetryDelayMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogFavoritePlaybackSkipped(request, "wait-delay", "cancelled");
                return;
            }

            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-wait-delay"))
            {
                return;
            }

            var state = await ExecuteUnifiedPlayerControlOnUiAsync(
                IpcConstants.CommandPlayerGetState,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-state"))
            {
                return;
            }

            LogUnifiedPlayerControlResult("favorite-start-state", state);

            lastWaitResult = GetFavoritePlaybackWaitResult(request, state);
            LogFavoritePlaybackWait(request, attempt, lastWaitResult, state);

            if (!lastWaitResult.IsReady)
            {
                continue;
            }

            if (!TryValidateFavoriteStartPosition(request.StartPositionSeconds, state, out var validationMessage))
            {
                if (!TryContinueFavoritePlayback(request, cancellationToken, "before-range-error"))
                {
                    return;
                }

                CompletePendingFavoritePlayback(request);
                LogFavoritePlaybackFailed(request, "RangeValidationFailed", state.ErrorCode);
                ShowFavoritePlaybackError(validationMessage);
                return;
            }

            if (!TryContinueFavoritePlayback(request, cancellationToken, "before-muted"))
            {
                return;
            }

            var muteResult = await ExecuteUnifiedPlayerControlOnUiAsync(
                IpcConstants.CommandPlayerSetMuted,
                positionSeconds: null,
                volumePercent: null,
                muted: request.Mute,
                controlPolicy: null,
                cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-muted"))
            {
                return;
            }

            LogUnifiedPlayerControlResult("favorite-start-muted", muteResult);

            if (!await ApplyFavoriteLoopAsync(request, cancellationToken))
            {
                return;
            }

            if (!TryContinueFavoritePlayback(request, cancellationToken, "before-seek"))
            {
                return;
            }

            var seekResult = await ExecuteUnifiedPlayerControlOnUiAsync(
                IpcConstants.CommandPlayerSeek,
                positionSeconds: request.StartPositionSeconds,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-seek"))
            {
                return;
            }

            LogUnifiedPlayerControlResult("favorite-start-seek", seekResult);

            var verifiedState = await VerifyFavoriteSeekAsync(request, seekResult, cancellationToken);
            if (verifiedState is null)
            {
                if (!TryContinueFavoritePlayback(request, cancellationToken, "after-seek-verify"))
                {
                    return;
                }

                CompletePendingFavoritePlayback(request);
                LogFavoriteSeekVerificationFailed(request, seekResult);
                LogFavoritePlaybackFailed(request, "SeekVerificationFailed", seekResult.ErrorCode);
                ShowFavoritePlaybackError(AppConstants.FavoritePlaybackPositionUnknownMessage);
                return;
            }

            var finalCommand = request.Autoplay
                ? IpcConstants.CommandPlayerPlay
                : IpcConstants.CommandPlayerPause;
            if (!TryContinueFavoritePlayback(request, cancellationToken, "before-final-command"))
            {
                return;
            }

            var finalResult = await ExecuteUnifiedPlayerControlOnUiAsync(
                finalCommand,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-final-command"))
            {
                return;
            }

            LogUnifiedPlayerControlResult(request.Autoplay ? "favorite-start-play" : "favorite-start-pause", finalResult);
            LogFavoritePlaybackCompleted(request, seekResult, finalResult);

            CompletePendingFavoritePlayback(request);
            return;
        }

        if (ReferenceEquals(_pendingFavoritePlaybackRequest, request))
        {
            CompletePendingFavoritePlayback(request);
            LogFavoritePlaybackTimeout(request, lastWaitResult);
            LogFavoritePlaybackFailed(request, "WaitTimeout", IpcConstants.ErrorCodeTimeout);
            ShowFavoritePlaybackError(AppConstants.FavoritePlaybackPositionUnknownMessage);
        }
    }

    private async Task<bool> ApplyFavoriteLoopAsync(
        FavoritePlaybackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryContinueFavoritePlayback(request, cancellationToken, "before-loop"))
            {
                return false;
            }

            if (PlayerWebView.CoreWebView2 is null)
            {
                return false;
            }

            var loop = request.Loop;
            var loopLiteral = loop ? "true" : "false";
            var script = $$"""
(() => {
  const requestedLoop = {{loopLiteral}};
  const media = Array.from(document.querySelectorAll("video,audio"))
    .filter((element) => element && element.currentSrc !== undefined);
  const target = media.find((element) => !element.paused) || media[0] || null;
  if (!target) return "media-not-found";
  target.loop = requestedLoop;
  return target.loop === requestedLoop ? "loop-set" : "loop-not-set";
})()
""";
            var result = await PlayerWebView.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-loop"))
            {
                return false;
            }

            DiagnosticLogService.Write("Favorite", $"Event=FavoriteLoopApply; RequestedLoop={loop}; Result={result}");
            return true;
        }
        catch (OperationCanceledException)
        {
            LogFavoritePlaybackSkipped(request, "loop", "cancelled");
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("Favorite", "Favorite loop apply failed.", ex);
            return false;
        }
    }

    private async Task<UnifiedPlayerStateResult?> VerifyFavoriteSeekAsync(
        FavoritePlaybackRequest request,
        UnifiedPlayerStateResult seekResult,
        CancellationToken cancellationToken)
    {
        if (IsFavoriteSeekVerified(seekResult, request.StartPositionSeconds))
        {
            return seekResult;
        }

        for (var attempt = 1; attempt <= FavoriteSeekVerifyMaxAttempts; attempt++)
        {
            try
            {
                await Task.Delay(FavoriteSeekVerifyRetryDelayMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            if (!IsCurrentFavoritePlaybackRequest(request))
            {
                return null;
            }

            var state = await ExecuteUnifiedPlayerControlOnUiAsync(
                IpcConstants.CommandPlayerGetState,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);
            if (!TryContinueFavoritePlayback(request, cancellationToken, "after-seek-verify-state"))
            {
                return null;
            }

            LogUnifiedPlayerControlResult("favorite-start-seek-verify", state);

            if (IsFavoriteSeekVerified(state, request.StartPositionSeconds))
            {
                return state;
            }

            DiagnosticLogService.Write(
                "Favorite",
                "Event=FavoriteSeekVerifyRetry"
                + "; RequestId=" + request.RequestId
                + "; Attempt=" + attempt
                + "; Requested=" + request.StartPositionSeconds
                + "; Actual=" + FormatNullable(state.CurrentTimeSeconds)
                + "; Tolerance=" + FavoriteSeekToleranceSeconds
                + "; Duration=" + FormatNullable(state.DurationSeconds)
                + "; SeekableStart=" + FormatNullable(state.SeekableStartSeconds)
                + "; SeekableEnd=" + FormatNullable(state.SeekableEndSeconds)
                + "; Success=" + state.Success
                + "; ErrorCode=" + (state.ErrorCode ?? string.Empty)
                + "; OperationResult=" + (state.OperationResult ?? string.Empty));
        }

        return null;
    }

    private FavoritePlaybackWaitResult GetFavoritePlaybackWaitResult(
        FavoritePlaybackRequest request,
        UnifiedPlayerStateResult state)
    {
        if (string.Equals(state.OperationResult, "empty-script-result", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.OperationError, "empty-script-result", StringComparison.OrdinalIgnoreCase))
        {
            return new FavoritePlaybackWaitResult(false, "empty-script-result");
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedYouTubeVideoId)
            && !IsCurrentYouTubeVideo(request.ExpectedYouTubeVideoId, state.CurrentUrl ?? GetCurrentWebViewUrl()))
        {
            return new FavoritePlaybackWaitResult(false, "target-url-not-ready");
        }

        if (!state.MediaFound)
        {
            return new FavoritePlaybackWaitResult(false, "video-element-not-found");
        }

        if (state.IsAdvertisement)
        {
            return new FavoritePlaybackWaitResult(false, "youtube-ad-showing");
        }

        if (state.ReadyState.GetValueOrDefault() < 1)
        {
            return new FavoritePlaybackWaitResult(false, "ready-state-insufficient");
        }

        if (request.StartPositionSeconds <= 0)
        {
            return new FavoritePlaybackWaitResult(true, "ready-no-start-position");
        }

        if (state.SeekableRangeCount > 0
            && state.SeekableStartSeconds.HasValue
            && state.SeekableEndSeconds.HasValue)
        {
            return new FavoritePlaybackWaitResult(true, "ready-seekable-range");
        }

        if (!state.IsLive && state.DurationSeconds.HasValue && state.DurationSeconds.Value > 0)
        {
            return new FavoritePlaybackWaitResult(true, "ready-duration");
        }

        return new FavoritePlaybackWaitResult(false, state.IsLive ? "live-seekable-not-ready" : "duration-or-seekable-not-ready");
    }

    private static bool TryGetYouTubeVideoId(string? url, out string videoId)
    {
        return YouTubeUrlService.TryGetVideoId(url, out videoId);
    }

    private static bool IsCurrentYouTubeVideo(string expectedVideoId, string? currentUrl)
    {
        return TryGetYouTubeVideoId(currentUrl, out var currentVideoId)
            && currentVideoId.Equals(expectedVideoId, StringComparison.Ordinal);
    }

    private static bool AreSameAbsoluteUrl(string? first, string? second)
    {
        if (!Uri.TryCreate(first, UriKind.Absolute, out var firstUri)
            || !Uri.TryCreate(second, UriKind.Absolute, out var secondUri))
        {
            return string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return Uri.Compare(
                firstUri,
                secondUri,
                UriComponents.SchemeAndServer
                | UriComponents.Path
                | UriComponents.Query
                | UriComponents.Fragment,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;
    }

    private bool IsCurrentFavoritePlaybackRequest(FavoritePlaybackRequest request)
    {
        return ReferenceEquals(_pendingFavoritePlaybackRequest, request);
    }

    private bool TryContinueFavoritePlayback(
        FavoritePlaybackRequest request,
        CancellationToken cancellationToken,
        string stage)
    {
        var isLatest = IsCurrentFavoritePlaybackRequest(request);
        var isCancelled = cancellationToken.IsCancellationRequested;
        if (isLatest && !isCancelled)
        {
            return true;
        }

        LogFavoritePlaybackSkipped(
            request,
            stage,
            isCancelled ? "cancelled" : "not-latest-request");
        return false;
    }

    private void CancelPendingFavoritePlayback()
    {
        _pendingFavoritePlaybackRequest = null;
        _favoritePlaybackCancellation?.Cancel();
        _favoritePlaybackCancellation?.Dispose();
        _favoritePlaybackCancellation = null;
    }

    private void CompletePendingFavoritePlayback(FavoritePlaybackRequest request)
    {
        if (!ReferenceEquals(_pendingFavoritePlaybackRequest, request))
        {
            return;
        }

        _pendingFavoritePlaybackRequest = null;
        _favoritePlaybackCancellation?.Dispose();
        _favoritePlaybackCancellation = null;
    }

    private static void LogFavoritePlaybackWait(
        FavoritePlaybackRequest request,
        int attempt,
        FavoritePlaybackWaitResult waitResult,
        UnifiedPlayerStateResult state)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackWait"
            + "; RequestId=" + request.RequestId
            + "; Attempt=" + attempt
            + "; Ready=" + waitResult.IsReady
            + "; Reason=" + waitResult.Reason
            + "; ExpectedVideoId=" + request.ExpectedYouTubeVideoId
            + "; Url=" + DiagnosticLogService.FormatUrlForLog(state.CurrentUrl ?? string.Empty)
            + "; MediaFound=" + state.MediaFound
            + "; ReadyState=" + (state.ReadyState?.ToString() ?? string.Empty)
            + "; Duration=" + FormatNullable(state.DurationSeconds)
            + "; Seekable=" + state.IsSeekable
            + "; SeekableCount=" + state.SeekableRangeCount
            + "; SeekableStart=" + FormatNullable(state.SeekableStartSeconds)
            + "; SeekableEnd=" + FormatNullable(state.SeekableEndSeconds)
            + "; IsLive=" + state.IsLive
            + "; IsAdvertisement=" + state.IsAdvertisement
            + "; MediaIdentity=" + (state.MediaIdentity ?? string.Empty)
            + "; MediaRevision=" + state.MediaRevision);
    }

    private static void LogFavoritePlaybackRequest(FavoritePlaybackRequest request)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackRequest"
            + "; RequestId=" + request.RequestId
            + "; Slot=" + request.SlotNumber
            + "; RegisteredUrl=" + DiagnosticLogService.FormatUrlForLog(request.Url)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(request.CurrentUrl)
            + "; RegisteredVideoId=" + request.ExpectedYouTubeVideoId
            + "; CurrentVideoId=" + request.CurrentYouTubeVideoId
            + "; SameUrl=" + request.SameUrl
            + "; SameVideo=" + request.SameVideo
            + "; NavigationSkipped=" + request.NavigationSkipped
            + "; StartPositionSeconds=" + request.StartPositionSeconds
            + "; SeekRequested=True"
            + "; SeekVerified="
            + "; AutoPlay=" + request.Autoplay
            + "; Muted=" + request.Mute
            + "; Loop=" + request.Loop
                + "; Result=Requested"
                + "; ErrorCode=");
    }

    private static void LogYouTubeVideoIdParse(
        string registeredUrl,
        string? currentUrl,
        string registeredVideoId,
        string currentVideoId,
        string registeredUrlType,
        string currentUrlType,
        bool sameVideo,
        bool registeredParseResult,
        bool currentParseResult,
        string registeredParseFailureReason,
        string currentParseFailureReason)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=YouTubeVideoIdParse"
            + "; RegisteredUrl=" + DiagnosticLogService.FormatUrlForLog(registeredUrl)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(currentUrl ?? string.Empty)
            + "; RegisteredVideoId=" + registeredVideoId
            + "; CurrentVideoId=" + currentVideoId
            + "; RegisteredUrlType=" + registeredUrlType
            + "; CurrentUrlType=" + currentUrlType
            + "; SameVideo=" + sameVideo
            + "; RegisteredParseResult=" + registeredParseResult
            + "; CurrentParseResult=" + currentParseResult
            + "; ParseResult=" + (registeredParseResult && currentParseResult)
            + "; RegisteredParseFailureReason=" + registeredParseFailureReason
            + "; CurrentParseFailureReason=" + currentParseFailureReason);
    }

    private static void LogFavoritePlaybackCompleted(
        FavoritePlaybackRequest request,
        UnifiedPlayerStateResult seekResult,
        UnifiedPlayerStateResult finalResult)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackCompleted"
            + "; RequestId=" + request.RequestId
            + "; Slot=" + request.SlotNumber
            + "; RegisteredUrl=" + DiagnosticLogService.FormatUrlForLog(request.Url)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(finalResult.CurrentUrl ?? request.CurrentUrl)
            + "; RegisteredVideoId=" + request.ExpectedYouTubeVideoId
            + "; CurrentVideoId=" + request.CurrentYouTubeVideoId
            + "; SameUrl=" + request.SameUrl
            + "; SameVideo=" + request.SameVideo
            + "; NavigationSkipped=" + request.NavigationSkipped
            + "; StartPositionSeconds=" + request.StartPositionSeconds
            + "; SeekRequested=True"
            + "; SeekVerified=" + IsFavoriteSeekVerified(seekResult, request.StartPositionSeconds)
            + "; ActualPosition=" + FormatNullable(seekResult.CurrentTimeSeconds)
            + "; AutoPlay=" + request.Autoplay
            + "; Muted=" + request.Mute
            + "; Loop=" + request.Loop
            + "; Result=" + (finalResult.Success ? "Success" : "Failed")
            + "; ErrorCode=" + (finalResult.ErrorCode ?? string.Empty));
    }

    private static void LogFavoritePlaybackFailed(
        FavoritePlaybackRequest request,
        string result,
        string? errorCode)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackFailed"
            + "; RequestId=" + request.RequestId
            + "; Slot=" + request.SlotNumber
            + "; RegisteredUrl=" + DiagnosticLogService.FormatUrlForLog(request.Url)
            + "; CurrentUrl=" + DiagnosticLogService.FormatUrlForLog(request.CurrentUrl)
            + "; RegisteredVideoId=" + request.ExpectedYouTubeVideoId
            + "; CurrentVideoId=" + request.CurrentYouTubeVideoId
            + "; SameUrl=" + request.SameUrl
            + "; SameVideo=" + request.SameVideo
            + "; NavigationSkipped=" + request.NavigationSkipped
            + "; StartPositionSeconds=" + request.StartPositionSeconds
            + "; SeekRequested=True"
            + "; SeekVerified=False"
            + "; AutoPlay=" + request.Autoplay
            + "; Muted=" + request.Mute
            + "; Loop=" + request.Loop
                + "; Result=" + result
                + "; ErrorCode=" + (errorCode ?? string.Empty));
    }

    private static void LogFavoritePlaybackSkipped(
        FavoritePlaybackRequest request,
        string stage,
        string skipReason)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackSkipped"
            + "; RequestId=" + request.RequestId
            + "; Slot=" + request.SlotNumber
            + "; Stage=" + stage
            + "; CancelRequested=" + (skipReason == "cancelled")
            + "; IsLatestRequest=False"
            + "; OperationSkipped=True"
            + "; SkipReason=" + skipReason
            + "; Result=Skipped");
    }

    private static void LogFavoritePlaybackTimeout(
        FavoritePlaybackRequest request,
        FavoritePlaybackWaitResult? waitResult)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoritePlaybackWaitTimeout"
            + "; RequestId=" + request.RequestId
            + "; LastReason=" + (waitResult?.Reason ?? string.Empty)
            + "; MaxAttempts=" + FavoritePlaybackMaxAttempts
            + "; RetryDelayMs=" + FavoritePlaybackRetryDelayMilliseconds);
    }

    private static void LogFavoriteSeekVerificationFailed(
        FavoritePlaybackRequest request,
        UnifiedPlayerStateResult seekResult)
    {
        DiagnosticLogService.Write(
            "Favorite",
            "Event=FavoriteSeekVerificationFailed"
            + "; RequestId=" + request.RequestId
            + "; Requested=" + request.StartPositionSeconds
            + "; Actual=" + FormatNullable(seekResult.CurrentTimeSeconds)
            + "; Tolerance=" + FavoriteSeekToleranceSeconds
            + "; Duration=" + FormatNullable(seekResult.DurationSeconds)
            + "; SeekableStart=" + FormatNullable(seekResult.SeekableStartSeconds)
            + "; SeekableEnd=" + FormatNullable(seekResult.SeekableEndSeconds)
            + "; AdapterSiteType=" + seekResult.SiteType
            + "; AdapterPlayerType=" + seekResult.PlayerType
            + "; Success=" + seekResult.Success
            + "; ErrorCode=" + (seekResult.ErrorCode ?? string.Empty)
            + "; OperationResult=" + (seekResult.OperationResult ?? string.Empty)
            + "; OperationError=" + (seekResult.OperationError ?? string.Empty));
    }

    private static bool TryValidateFavoriteStartPosition(
        int startPositionSeconds,
        UnifiedPlayerStateResult state,
        out string message)
    {
        message = string.Empty;
        if (startPositionSeconds <= 0)
        {
            return true;
        }

        if (!state.MediaFound)
        {
            message = AppConstants.FavoritePlaybackPositionUnknownMessage;
            return false;
        }

        if (!state.IsSeekable)
        {
            message = AppConstants.FavoritePlaybackRangeNotFoundMessage;
            return false;
        }

        if (state.SeekableRangeCount > 0)
        {
            if (!state.SeekableStartSeconds.HasValue || !state.SeekableEndSeconds.HasValue)
            {
                message = AppConstants.FavoritePlaybackPositionUnknownMessage;
                return false;
            }

            if (startPositionSeconds < state.SeekableStartSeconds.Value
                || startPositionSeconds > state.SeekableEndSeconds.Value)
            {
                message = CreateFavoritePlaybackRangeMessage(startPositionSeconds, state);
                return false;
            }

            return true;
        }

        if (!state.IsLive && state.DurationSeconds.HasValue)
        {
            if (startPositionSeconds >= state.DurationSeconds.Value)
            {
                message = CreateFavoritePlaybackRangeMessage(startPositionSeconds, state);
                return false;
            }

            return true;
        }

        message = AppConstants.FavoritePlaybackPositionUnknownMessage;
        return false;
    }

    private static string CreateFavoritePlaybackRangeMessage(int startPositionSeconds, UnifiedPlayerStateResult state)
    {
        var details = state.DurationSeconds.HasValue
            ? Environment.NewLine
                + "指定位置: "
                + FormatPlaybackTime(startPositionSeconds)
                + " / 動画の長さ: "
                + FormatPlaybackTime((int)Math.Floor(state.DurationSeconds.Value))
            : string.Empty;

        return AppConstants.FavoritePlaybackRangeNotFoundMessage + details;
    }

    private static bool IsFavoriteSeekVerified(UnifiedPlayerStateResult result, int startPositionSeconds)
    {
        return result.Success
            && result.MediaFound
            && result.CurrentTimeSeconds.HasValue
            && Math.Abs(result.CurrentTimeSeconds.Value - startPositionSeconds) <= FavoriteSeekToleranceSeconds;
    }

    private static void ShowFavoritePlaybackError(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            AppConstants.AppName,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static string FormatPlaybackTime(int totalSeconds)
    {
        var safeSeconds = Math.Max(0, totalSeconds);
        var hours = safeSeconds / 3600;
        var minutes = safeSeconds % 3600 / 60;
        var seconds = safeSeconds % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00}";
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

    private void NavigateTrusted(string url, bool saveAsLastUrl = true, bool preservePendingFavoritePlayback = false)
    {
        if (!preservePendingFavoritePlayback)
        {
            CancelPendingFavoritePlayback();
        }

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

        try
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            PlayerModeDragArea.Height = AppConstants.PlayerModeDragAreaHeight;
            PlayerModeDragArea.Visibility = Visibility.Visible;
            LoadingStatusText.Text = AppConstants.PlayerModeChromeHiddenText;
            DiagnosticLogService.Write(
                AppConstants.WindowStateLogCategory,
                CreatePlayerModeChromeLog("StartupPlayerModeApplied", null, string.Empty));
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException(AppConstants.WindowStateLogCategory, "Player mode chrome hide failed.", ex);
            LoadingStatusText.Text = AppConstants.PlayerModeChromeHideFailedText;
        }

        TopMenu.Visibility = Visibility.Collapsed;
        AddressBarPanel.Visibility = Visibility.Collapsed;
        FavoriteButtonPanel.Visibility = Visibility.Collapsed;
        StatusBarArea.Visibility = Visibility.Collapsed;
        ToolbarRow.Height = new GridLength(0);
        FavoriteButtonRow.Height = new GridLength(0);
        StatusBarRow.Height = new GridLength(0);
        ApplyPlayerModeContentFrame();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (!_isPlayerMode)
        {
            return;
        }

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(PlayerModeWindowProc);
            ApplyPlayerModeNativeChrome(source.Handle, "SourceInitialized");
        }
    }

    private void ApplyPlayerModeContentFrame()
    {
        if (!_isPlayerMode)
        {
            return;
        }

        Background = System.Windows.Media.Brushes.Black;
        RootGrid.Background = System.Windows.Media.Brushes.Black;
        PlayerArea.Background = System.Windows.Media.Brushes.Black;
        PlayerArea.Margin = new Thickness(0);
        PlayerArea.Padding = new Thickness(0);
        PlayerArea.BorderThickness = new Thickness(0);
        PlayerArea.CornerRadius = new CornerRadius(0);
        PlayerWebView.Margin = new Thickness(0);

        DiagnosticLogService.Write(
            AppConstants.WindowStateLogCategory,
            CreatePlayerModeContentFrameLog("PlayerModeContentFrameApplied"));
    }

    private void ApplyPlayerModeNativeChrome(IntPtr hwnd, string eventName)
    {
        if (!_isPlayerMode)
        {
            return;
        }

        if (hwnd == IntPtr.Zero)
        {
            DiagnosticLogService.Write(
                AppConstants.WindowStateLogCategory,
                CreatePlayerModeChromeLog(eventName, null, "HwndUnavailable"));
            return;
        }

        try
        {
            var beforeStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64();
            var beforeExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
            var requestedStyle = beforeStyle & ~PlayerModeRemoveStyleMask;
            var requestedExStyle = beforeExStyle & ~PlayerModeRemoveExStyleMask;

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, new IntPtr(requestedStyle));
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(requestedExStyle));
            var cornerPreference = NativeMethods.DwmWindowCornerPreferenceDoNotRound;
            var dwmCornerResult = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmWindowCornerPreference,
                ref cornerPreference,
                sizeof(int));
            var borderColor = NativeMethods.DwmColorNone;
            var dwmBorderColorResult = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmBorderColor,
                ref borderColor,
                sizeof(int));
            var captionColor = NativeMethods.DwmColorNone;
            var dwmCaptionColorResult = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmCaptionColor,
                ref captionColor,
                sizeof(int));
            var textColor = NativeMethods.DwmColorNone;
            var dwmTextColorResult = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmTextColor,
                ref textColor,
                sizeof(int));
            var frameChanged = NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove
                    | NativeMethods.SwpNoSize
                    | NativeMethods.SwpNoZOrder
                    | NativeMethods.SwpNoActivate
                    | NativeMethods.SwpFrameChanged);
            var verifiedStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64();
            var verifiedExStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
            var borderlessFrameApplied =
                (verifiedStyle & PlayerModeRemoveStyleMask) == 0
                && (verifiedExStyle & PlayerModeRemoveExStyleMask) == 0
                && frameChanged
                && dwmCornerResult == 0
                && dwmBorderColorResult == 0;
            var result = new PlayerModeChromeApplyResult(
                beforeStyle,
                requestedStyle,
                verifiedStyle,
                beforeExStyle,
                requestedExStyle,
                verifiedExStyle,
                frameChanged,
                dwmCornerResult,
                dwmBorderColorResult,
                dwmCaptionColorResult,
                dwmTextColorResult,
                borderlessFrameApplied);

            DiagnosticLogService.Write(
                AppConstants.WindowStateLogCategory,
                CreatePlayerModeChromeLog(eventName, result, string.Empty));
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException(
                AppConstants.WindowStateLogCategory,
                CreatePlayerModeChromeLog(eventName, null, ex.GetType().Name),
                ex);
            LoadingStatusText.Text = AppConstants.PlayerModeChromeHideFailedText;
        }
    }

    private string CreatePlayerModeChromeLog(
        string eventName,
        PlayerModeChromeApplyResult? result,
        string skipReason)
    {
        return "Event=" + eventName
            + "; PID=" + Environment.ProcessId
            + "; PlayerMode=" + _isPlayerMode
            + "; WindowStyle=" + WindowStyle
            + "; ResizeMode=" + ResizeMode
            + "; WindowChrome=" + AppConstants.PlayerModeWindowChromeNoneText
            + "; StyleBefore=0x" + FormatHex(result?.StyleBefore)
            + "; StyleRequested=0x" + FormatHex(result?.StyleRequested)
            + "; StyleAfter=0x" + FormatHex(result?.StyleAfter)
            + "; ExStyleBefore=0x" + FormatHex(result?.ExStyleBefore)
            + "; ExStyleRequested=0x" + FormatHex(result?.ExStyleRequested)
            + "; ExStyleAfter=0x" + FormatHex(result?.ExStyleAfter)
            + "; TargetStylesRemoved=" + (result?.TargetStylesRemoved.ToString() ?? string.Empty)
            + "; TargetExStylesRemoved=" + (result?.TargetExStylesRemoved.ToString() ?? string.Empty)
            + "; FrameChanged=" + (result?.FrameChanged.ToString() ?? string.Empty)
            + "; DwmCornerResult=" + FormatHresult(result?.DwmCornerResult)
            + "; DwmBorderColorResult=" + FormatHresult(result?.DwmBorderColorResult)
            + "; DwmCaptionColorResult=" + FormatHresult(result?.DwmCaptionColorResult)
            + "; DwmTextColorResult=" + FormatHresult(result?.DwmTextColorResult)
            + "; BorderlessFrameApplied=" + (result?.BorderlessFrameApplied.ToString() ?? string.Empty)
            + "; IsFullScreen=" + IsFullScreen
            + "; SkipReason=" + skipReason;
    }

    private string CreatePlayerModeContentFrameLog(string eventName)
    {
        return "Event=" + eventName
            + "; PID=" + Environment.ProcessId
            + "; PlayerMode=" + _isPlayerMode
            + "; WindowBackground=" + FormatBrushForLog(Background)
            + "; RootGridBackground=" + FormatBrushForLog(RootGrid.Background)
            + "; PlayerAreaBackground=" + FormatBrushForLog(PlayerArea.Background)
            + "; PlayerAreaBorderBrush=" + FormatBrushForLog(PlayerArea.BorderBrush)
            + "; PlayerAreaBorderThickness=" + PlayerArea.BorderThickness
            + "; PlayerAreaMargin=" + PlayerArea.Margin
            + "; PlayerAreaPadding=" + PlayerArea.Padding
            + "; PlayerAreaCornerRadius=" + PlayerArea.CornerRadius
            + "; PlayerWebViewMargin=" + PlayerWebView.Margin
            + "; TopMenuVisibility=" + TopMenu.Visibility
            + "; AddressBarPanelVisibility=" + AddressBarPanel.Visibility
            + "; FavoriteButtonPanelVisibility=" + FavoriteButtonPanel.Visibility
            + "; StatusBarAreaVisibility=" + StatusBarArea.Visibility;
    }

    private static string FormatBrushForLog(System.Windows.Media.Brush? brush)
    {
        return brush switch
        {
            SolidColorBrush solidColorBrush => solidColorBrush.Color.ToString(),
            null => string.Empty,
            _ => brush.GetType().Name
        };
    }

    private static string FormatHex(long? value)
    {
        return value.HasValue ? value.Value.ToString("X") : string.Empty;
    }

    private static string FormatHresult(int? value)
    {
        return value.HasValue ? "0x" + value.Value.ToString("X8") : string.Empty;
    }

    private void PlayerModeDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPlayerMode || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException ex)
        {
            DiagnosticLogService.WriteException("WindowState", "Player mode DragMove failed.", ex);
        }
    }

    private IntPtr PlayerModeWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_isPlayerMode || msg != WmNcHitTest || IsFullScreen)
        {
            return IntPtr.Zero;
        }

        var hitTest = HitTestPlayerModeWindow(lParam);
        if (hitTest == HtClient)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private int HitTestPlayerModeWindow(IntPtr lParam)
    {
        var point = PointFromScreen(new System.Windows.Point(GetSignedLowWord(lParam), GetSignedHighWord(lParam)));
        var border = AppConstants.PlayerModeResizeBorderThickness;
        var left = point.X <= border;
        var right = point.X >= ActualWidth - border;
        var top = point.Y <= border;
        var bottom = point.Y >= ActualHeight - border;

        if (top && left)
        {
            return HtTopLeft;
        }

        if (top && right)
        {
            return HtTopRight;
        }

        if (bottom && left)
        {
            return HtBottomLeft;
        }

        if (bottom && right)
        {
            return HtBottomRight;
        }

        if (left)
        {
            return HtLeft;
        }

        if (right)
        {
            return HtRight;
        }

        if (top)
        {
            return HtTop;
        }

        if (bottom)
        {
            return HtBottom;
        }

        return Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
            ? HtCaption
            : HtClient;
    }

    private static int GetSignedLowWord(IntPtr value)
    {
        return unchecked((short)((long)value & 0xFFFF));
    }

    private static int GetSignedHighWord(IntPtr value)
    {
        return unchecked((short)(((long)value >> 16) & 0xFFFF));
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

        StartResizeDiagnostics("ResizeStarted");
        UpdateWindowSizeMenuChecks(GetCurrentWindowSizePresetForMenu());
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("WindowStateChanged", null, null));
        if (!IsFullScreen)
        {
            StartResizeDiagnostics("WindowStateChanged");
        }
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsFullScreen)
        {
            StartResizeDiagnostics("LocationChanged");
        }
    }

    private void StartResizeDiagnostics(string eventName)
    {
        if (_pendingResizeSnapshot is null)
        {
            var snapshot = CaptureWindowTransitionSnapshot(eventName, null);
            _pendingResizeSnapshot = snapshot;
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog(eventName, snapshot, null));
            _ = UpdatePendingResizeMediaStateAsync(snapshot);
        }

        _resizeDiagnosticsDebounceTimer.Stop();
        _resizeDiagnosticsDebounceTimer.Start();
    }

    private async Task UpdatePendingResizeMediaStateAsync(WindowTransitionSnapshot snapshot)
    {
        var mediaState = await InspectMediaStateAsync();
        if (ReferenceEquals(_pendingResizeSnapshot, snapshot))
        {
            _pendingResizeSnapshot = snapshot with { MediaState = mediaState };
        }
    }

    private async void ResizeDiagnosticsDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _resizeDiagnosticsDebounceTimer.Stop();
        var before = _pendingResizeSnapshot;
        _pendingResizeSnapshot = null;
        if (before is null)
        {
            return;
        }

        await CompleteWindowTransitionDiagnosticsAsync("ResizeCompleted", before, restorePausedState: true);
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

    private async void ToggleFullScreen()
    {
        if (_isFullScreenTransitioning)
        {
            return;
        }

        if (IsFullScreen)
        {
            await ExitFullScreenAsync();
        }
        else
        {
            await EnterFullScreenAsync();
        }
    }

    private async Task EnterFullScreenAsync()
    {
        if (IsFullScreen || _isFullScreenTransitioning)
        {
            return;
        }

        _isFullScreenTransitioning = true;
        try
        {
            var before = await CaptureWindowTransitionSnapshotAsync("FullscreenEnterStarted");
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("FullscreenEnterStarted", before, null));
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
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await CompleteWindowTransitionDiagnosticsAsync("FullscreenEnterCompleted", before, restorePausedState: true);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("WindowState", "FullscreenEnter failed.", ex);
        }
        finally
        {
            _isFullScreenTransitioning = false;
        }
    }

    private async Task ExitFullScreenAsync()
    {
        if (_fullScreenSnapshot is not { } snapshot)
        {
            return;
        }

        if (_isFullScreenTransitioning)
        {
            return;
        }

        _isFullScreenTransitioning = true;
        try
        {
            var before = await CaptureWindowTransitionSnapshotAsync("FullscreenExitStarted");
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("FullscreenExitStarted", before, null));
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
            if (_isPlayerMode)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.CanResize;
                PlayerModeDragArea.Height = AppConstants.PlayerModeDragAreaHeight;
                PlayerModeDragArea.Visibility = Visibility.Visible;
                ApplyPlayerModeContentFrame();
                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    ApplyPlayerModeNativeChrome(source.Handle, "FullscreenExitReapply");
                }
            }

            LoadingStatusText.Text = AppConstants.FullScreenOffText;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await CompleteWindowTransitionDiagnosticsAsync("FullscreenExitCompleted", before, restorePausedState: true);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("WindowState", "FullscreenExit failed.", ex);
        }
        finally
        {
            _isFullScreenTransitioning = false;
        }
    }

    private void EnterFullScreen()
    {
        _ = EnterFullScreenAsync();
    }

    private void ExitFullScreen()
    {
        _ = ExitFullScreenAsync();
    }

    private async Task<WindowTransitionSnapshot> CaptureWindowTransitionSnapshotAsync(string eventName)
    {
        return CaptureWindowTransitionSnapshot(eventName, await InspectMediaStateAsync());
    }

    private WindowTransitionSnapshot CaptureWindowTransitionSnapshot(string eventName, MediaControlResult? mediaState)
    {
        _navigationStartedDuringWindowTransition = false;
        _navigationCompletedDuringWindowTransition = false;
        return new WindowTransitionSnapshot(
            eventName,
            DateTime.Now,
            GetCurrentWebViewUrl() ?? string.Empty,
            Left,
            Top,
            Width,
            Height,
            ActualWidth,
            ActualHeight,
            WindowState,
            IsFullScreen,
            mediaState);
    }

    private async Task CompleteWindowTransitionDiagnosticsAsync(
        string eventName,
        WindowTransitionSnapshot before,
        bool restorePausedState)
    {
        var stopwatch = Stopwatch.StartNew();
        MediaControlResult? after = null;
        try
        {
            after = await InspectMediaStateAsync();
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("MediaStateBefore", before, before.MediaState));
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("MediaStateAfter", before, after));
            await RestorePlaybackStateIfNeededAsync(before, after, restorePausedState);
            LogWindowTransitionAnomalies(before, after);
            if (eventName == "ResizeCompleted")
            {
                await RefreshShortsRenderAfterResizeAsync(before);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("WindowState", eventName + " diagnostics failed.", ex);
        }
        finally
        {
            stopwatch.Stop();
            DiagnosticLogService.Write(
                "WindowState",
                CreateWindowTransitionLog(eventName, before, after)
                + "; DurationMs=" + stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<MediaControlResult?> InspectMediaStateAsync()
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return null;
        }

        try
        {
            return await MediaControlService.InspectAsync(PlayerWebView.CoreWebView2, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("WindowState", "Media inspect failed.", ex);
            return null;
        }
    }

    private async Task RestorePlaybackStateIfNeededAsync(
        WindowTransitionSnapshot before,
        MediaControlResult? after,
        bool restorePausedState)
    {
        if (!restorePausedState
            || before.MediaState is not { MediaFound: true, IsPaused: true } beforeMedia
            || after is not { MediaFound: true, IsPaused: false })
        {
            return;
        }

        if (!IsSameMedia(beforeMedia, after))
        {
            return;
        }

        var pauseResult = await MediaControlService.ExecuteAsync(
            PlayerWebView.CoreWebView2!,
            IpcConstants.CommandPause,
            CancellationToken.None);
        _lastMediaCommand = IpcConstants.CommandPause;
        _lastMediaControlResult = pauseResult;
        LogMediaControlResult(IpcConstants.CommandPause, pauseResult);
        DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("MediaStateRestored", before, pauseResult));
    }

    private void LogWindowTransitionAnomalies(WindowTransitionSnapshot before, MediaControlResult? after)
    {
        var currentUrl = GetCurrentWebViewUrl() ?? string.Empty;
        if (!string.Equals(before.CurrentUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
        {
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("UrlChanged", before, after));
        }

        if (before.MediaState is { MediaFound: true } beforeMedia && after is { MediaFound: true })
        {
            if (!IsSameMedia(beforeMedia, after))
            {
                DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("ShortsIdentityChanged", before, after));
            }

            if (beforeMedia.IsPaused != after.IsPaused)
            {
                DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("UnexpectedPlaybackStateChanged", before, after));
            }
        }

        if (after is { MediaFound: true, IsPaused: false }
            && ((after.VideoWidth ?? 0) <= 0
                || (after.VideoHeight ?? 0) <= 0
                || (after.DisplayWidth ?? 0) <= 0
                || (after.DisplayHeight ?? 0) <= 0
                || string.Equals(after.Display, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(after.Visibility, "hidden", StringComparison.OrdinalIgnoreCase)))
        {
            DiagnosticLogService.Write("WindowState", CreateWindowTransitionLog("BlackVideoSuspected", before, after));
        }
    }

    private static bool IsSameMedia(MediaControlResult before, MediaControlResult after)
    {
        if (string.IsNullOrWhiteSpace(before.CurrentSrc) || string.IsNullOrWhiteSpace(after.CurrentSrc))
        {
            return true;
        }

        return string.Equals(before.CurrentSrc, after.CurrentSrc, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshShortsRenderAfterResizeAsync(WindowTransitionSnapshot resizeSnapshot)
    {
        var stopwatch = Stopwatch.StartNew();
        MediaControlResult? before = null;
        var snapshot = resizeSnapshot;
        try
        {
            before = await InspectMediaStateAsync();
            snapshot = resizeSnapshot with { MediaState = before };
            WriteRenderRefreshLog("RenderRefreshRequested", snapshot, before, "None", "Requested", string.Empty, stopwatch);

            var skipReason = GetRenderRefreshSkipReason(before);
            if (!string.IsNullOrEmpty(skipReason))
            {
                WriteRenderRefreshLog("RenderRefreshSkipped", snapshot, before, "None", "Skipped", skipReason, stopwatch);
                return;
            }

            WriteRenderRefreshLog("RenderRefreshStep1Started", snapshot, before, "WpfLayoutRefresh", "Started", string.Empty, stopwatch);
            PlayerArea.InvalidateMeasure();
            PlayerArea.InvalidateArrange();
            PlayerArea.InvalidateVisual();
            PlayerWebView.InvalidateMeasure();
            PlayerWebView.InvalidateArrange();
            PlayerWebView.InvalidateVisual();
            await Dispatcher.InvokeAsync(
                () =>
                {
                    PlayerArea.UpdateLayout();
                    PlayerWebView.UpdateLayout();
                },
                DispatcherPriority.Render);
            await Task.Delay(50);
            var afterStep1 = await InspectMediaStateAsync();
            WriteRenderRefreshLog("RenderRefreshStep1Completed", snapshot, afterStep1, "WpfLayoutRefresh", "Completed", string.Empty, stopwatch);
            LogUnexpectedRenderRefreshStateChange(snapshot, before, afterStep1);

            WriteRenderRefreshLog("RenderRefreshStep2Started", snapshot, afterStep1, "VideoCssRefresh", "Started", string.Empty, stopwatch);
            var scriptResult = await ExecuteShortsVideoRenderRefreshScriptAsync();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(50);
            var afterStep2 = await InspectMediaStateAsync();
            WriteRenderRefreshLog("RenderRefreshStep2Completed", snapshot, afterStep2, "VideoCssRefresh", scriptResult, string.Empty, stopwatch);
            LogUnexpectedRenderRefreshStateChange(snapshot, before, afterStep2);
        }
        catch (Exception ex)
        {
            DiagnosticLogService.WriteException("WindowState", "Render refresh failed.", ex);
            WriteRenderRefreshLog("RenderRefreshSkipped", snapshot, before, "Unknown", "Failed", ex.GetType().Name, stopwatch);
        }
    }

    private string GetRenderRefreshSkipReason(MediaControlResult? media)
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return "CoreWebView2Unavailable";
        }

        if (WindowState == WindowState.Minimized)
        {
            return "WindowMinimized";
        }

        if (PlayerWebView.ActualWidth <= 0 || PlayerWebView.ActualHeight <= 0)
        {
            return "WebViewSizeZero";
        }

        if (!IsCurrentShortsUrl())
        {
            return "NotShortsUrl";
        }

        if (!_navigationCompleted)
        {
            return "NavigationInProgress";
        }

        if (media is null)
        {
            return "MediaStateUnavailable";
        }

        if (!media.MediaFound)
        {
            return "MediaNotFound";
        }

        if (!string.Equals(media.TargetElementTag, "video", StringComparison.OrdinalIgnoreCase))
        {
            return "TargetIsNotVideo";
        }

        if (media.IsPaused is null)
        {
            return "PausedStateUnavailable";
        }

        if ((media.DisplayWidth ?? 0) <= 0 || (media.DisplayHeight ?? 0) <= 0)
        {
            return "VideoDisplaySizeZero";
        }

        if (string.Equals(media.Display, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(media.Visibility, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            return "VideoNotVisible";
        }

        return string.Empty;
    }

    private bool IsCurrentShortsUrl()
    {
        var currentUrl = GetCurrentWebViewUrl();
        return Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.Contains("/shorts/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ExecuteShortsVideoRenderRefreshScriptAsync()
    {
        if (PlayerWebView.CoreWebView2 is null)
        {
            return "CoreWebView2Unavailable";
        }

        const string script = """
(() => {
  const area = (element) => {
    const rect = element.getBoundingClientRect();
    return Math.max(0, rect.width) * Math.max(0, rect.height);
  };

  const score = (element) => {
    const style = window.getComputedStyle(element);
    const visible = style.display !== "none" && style.visibility !== "hidden" && area(element) > 0;
    const hasSource = Boolean(element.currentSrc || element.src);
    let value = 0;
    if (!element.paused) value += 800;
    if (visible) value += 600;
    if (element.readyState >= 2) value += 300;
    if (hasSource) value += 200;
    value += Math.min(area(element), 100000) / 1000;
    if (!visible) value -= 500;
    return value;
  };

  const target = Array.from(document.querySelectorAll("video"))
    .sort((a, b) => score(b) - score(a))[0] || null;

  if (!target) {
    return JSON.stringify({ success: false, reason: "VideoNotFound" });
  }

  const style = window.getComputedStyle(target);
  const rect = target.getBoundingClientRect();
  if (style.display === "none" || style.visibility === "hidden" || rect.width <= 0 || rect.height <= 0) {
    return JSON.stringify({ success: false, reason: "VideoNotVisible" });
  }

  const previousTransform = target.style.transform;
  const previousWillChange = target.style.willChange;
  target.style.willChange = "transform";
  target.style.transform = `${previousTransform ? previousTransform + " " : ""}translateZ(0) scale(1.000001)`;
  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      target.style.transform = previousTransform;
      target.style.willChange = previousWillChange;
    });
  });

  return JSON.stringify({
    success: true,
    reason: "CssTransformRefreshQueued",
    paused: target.paused,
    currentTime: Number.isFinite(target.currentTime) ? target.currentTime : null,
    readyState: target.readyState
  });
})()
""";

        var resultJson = await PlayerWebView.CoreWebView2.ExecuteScriptAsync(script);
        return DecodeScriptStringResult(resultJson);
    }

    private static string DecodeScriptStringResult(string resultJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(resultJson) ?? resultJson;
        }
        catch (JsonException)
        {
            return resultJson;
        }
    }

    private void LogUnexpectedRenderRefreshStateChange(
        WindowTransitionSnapshot snapshot,
        MediaControlResult? before,
        MediaControlResult? after)
    {
        if (!HasUnexpectedRenderRefreshStateChange(snapshot, before, after))
        {
            return;
        }

        DiagnosticLogService.Write(
            "WindowState",
            CreateWindowTransitionLog("RenderRefreshStateChangedUnexpectedly", snapshot with { MediaState = before }, after));
    }

    private bool HasUnexpectedRenderRefreshStateChange(
        WindowTransitionSnapshot snapshot,
        MediaControlResult? before,
        MediaControlResult? after)
    {
        var currentUrl = GetCurrentWebViewUrl() ?? string.Empty;
        if (!string.Equals(snapshot.CurrentUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (before is null || after is null)
        {
            return false;
        }

        if (before.MediaFound && after.MediaFound && !IsSameMedia(before, after))
        {
            return true;
        }

        if (before.IsPaused != after.IsPaused)
        {
            return true;
        }

        if (before.CurrentTime is not { } beforeTime || after.CurrentTime is not { } afterTime)
        {
            return false;
        }

        var delta = Math.Abs(afterTime - beforeTime);
        if (before.IsPaused == true)
        {
            return delta > 0.5;
        }

        return afterTime + 0.25 < beforeTime || delta > 5;
    }

    private void WriteRenderRefreshLog(
        string eventName,
        WindowTransitionSnapshot snapshot,
        MediaControlResult? media,
        string method,
        string result,
        string skipReason,
        Stopwatch stopwatch)
    {
        DiagnosticLogService.Write(
            "WindowState",
            CreateWindowTransitionLog(eventName, snapshot, media)
            + "; RefreshMethod=" + method
            + "; RefreshResult=" + SanitizeLogValue(result)
            + "; SkipReason=" + skipReason
            + "; DurationMs=" + stopwatch.ElapsedMilliseconds);
    }

    private static string SanitizeLogValue(string value)
    {
        return value
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private string CreateWindowTransitionLog(
        string eventName,
        WindowTransitionSnapshot? before,
        MediaControlResult? media)
    {
        var currentUrl = DiagnosticLogService.FormatUrlForLog(GetCurrentWebViewUrl());
        return "Event=" + eventName
            + "; PID=" + Environment.ProcessId
            + "; AppVersion=" + AppConstants.AppVersion
            + "; WindowMode=" + (IsFullScreen ? "FullScreen" : WindowState.ToString())
            + "; BeforeLeft=" + FormatNullable(before?.Left)
            + "; BeforeTop=" + FormatNullable(before?.Top)
            + "; BeforeWidth=" + FormatNullable(before?.Width)
            + "; BeforeHeight=" + FormatNullable(before?.Height)
            + "; AfterLeft=" + FormatNullable(Left)
            + "; AfterTop=" + FormatNullable(Top)
            + "; AfterWidth=" + FormatNullable(Width)
            + "; AfterHeight=" + FormatNullable(Height)
            + "; WebViewActualWidth=" + FormatNullable(PlayerWebView.ActualWidth)
            + "; WebViewActualHeight=" + FormatNullable(PlayerWebView.ActualHeight)
            + "; CurrentUrl=" + currentUrl
            + "; UrlChanged=" + (!string.IsNullOrWhiteSpace(before?.CurrentUrl)
                && !string.Equals(before.CurrentUrl, GetCurrentWebViewUrl(), StringComparison.OrdinalIgnoreCase))
            + "; NavigationStartingDuringTransition=" + _navigationStartedDuringWindowTransition
            + "; NavigationCompletedDuringTransition=" + _navigationCompletedDuringWindowTransition
            + "; ReadyState=" + (media?.DocumentReadyState ?? string.Empty)
            + "; VideoCount=" + (media?.VideoElementCount ?? 0)
            + "; AudioCount=" + (media?.AudioElementCount ?? 0)
            + "; IframeCount=" + (media?.IframeElementCount ?? 0)
            + "; CurrentSrc=" + DiagnosticLogService.FormatUrlForLog(media?.CurrentSrc)
            + "; Paused=" + FormatNullable(media?.IsPaused)
            + "; Muted=" + FormatNullable(media?.IsMuted)
            + "; CurrentTime=" + FormatNullable(media?.CurrentTime)
            + "; Duration=" + FormatNullable(media?.Duration)
            + "; MediaReadyState=" + (media?.ReadyState?.ToString() ?? string.Empty)
            + "; NetworkState=" + (media?.NetworkState?.ToString() ?? string.Empty)
            + "; VideoWidth=" + (media?.VideoWidth?.ToString() ?? string.Empty)
            + "; VideoHeight=" + (media?.VideoHeight?.ToString() ?? string.Empty)
            + "; DisplayWidth=" + FormatNullable(media?.DisplayWidth)
            + "; DisplayHeight=" + FormatNullable(media?.DisplayHeight)
            + "; Display=" + (media?.Display ?? string.Empty)
            + "; Visibility=" + (media?.Visibility ?? string.Empty)
            + "; Opacity=" + (media?.Opacity ?? string.Empty)
            + "; SameMedia=" + (before?.MediaState is null || media is null
                ? string.Empty
                : IsSameMedia(before.MediaState, media).ToString());
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

    private sealed record WindowTransitionSnapshot(
        string EventName,
        DateTime Timestamp,
        string CurrentUrl,
        double Left,
        double Top,
        double Width,
        double Height,
        double ActualWidth,
        double ActualHeight,
        WindowState WindowState,
        bool IsFullScreen,
        MediaControlResult? MediaState);

    private sealed record FavoritePlaybackRequest(
        Guid RequestId,
        int SlotNumber,
        string Url,
        string ExpectedYouTubeVideoId,
        string CurrentUrl,
        string CurrentYouTubeVideoId,
        bool SameUrl,
        bool SameVideo,
        bool NavigationSkipped,
        int StartPositionSeconds,
        bool Autoplay,
        bool Mute,
        bool Loop);

    private sealed record FavoritePlaybackWaitResult(
        bool IsReady,
        string Reason);

    private sealed record PlayerModeChromeApplyResult(
        long StyleBefore,
        long StyleRequested,
        long StyleAfter,
        long ExStyleBefore,
        long ExStyleRequested,
        long ExStyleAfter,
        bool FrameChanged,
        int DwmCornerResult,
        int DwmBorderColorResult,
        int DwmCaptionColorResult,
        int DwmTextColorResult,
        bool BorderlessFrameApplied)
    {
        public bool TargetStylesRemoved => (StyleAfter & PlayerModeRemoveStyleMask) == 0;

        public bool TargetExStylesRemoved => (ExStyleAfter & PlayerModeRemoveExStyleMask) == 0;
    }
}
