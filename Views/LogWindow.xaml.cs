using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services;

namespace LiteTubeDock.Views;

public partial class LogWindow : Window
{
    private readonly LogFileService _logFileService = new();
    private readonly DispatcherTimer _autoRefreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private LogFileSnapshot? _currentSnapshot;
    private bool _isLoading;
    private int _lastSearchIndex = -1;

    public LogWindow()
    {
        InitializeComponent();

        Title = AppConstants.LogViewerWindowTitle;
        ReloadLogButton.Content = AppConstants.LogViewerReloadButtonText;
        CopyAllLogButton.Content = AppConstants.LogViewerCopyAllButtonText;
        OpenLogFolderButton.Content = AppConstants.LogViewerOpenFolderButtonText;
        CloseLogWindowButton.Content = AppConstants.CloseButtonText;
        AutoRefreshCheckBox.Content = AppConstants.LogViewerAutoRefreshText;
        ScrollToEndCheckBox.Content = AppConstants.LogViewerScrollToEndText;
        FindNextButton.Content = AppConstants.LogViewerFindNextButtonText;
        SearchTextBox.ToolTip = AppConstants.LogViewerSearchToolTip;
        AutoRefreshCheckBox.IsChecked = true;
        ScrollToEndCheckBox.IsChecked = false;

        ReloadLogButton.Click += async (_, _) => await LoadLatestLogAsync(force: true);
        CopyAllLogButton.Click += (_, _) => CopyAllLog();
        OpenLogFolderButton.Click += (_, _) => OpenLogFolder();
        CloseLogWindowButton.Click += (_, _) => Close();
        FindNextButton.Click += (_, _) => FindNext();
        SearchTextBox.KeyDown += SearchTextBox_KeyDown;
        PreviewKeyDown += LogWindow_PreviewKeyDown;
        _autoRefreshTimer.Tick += async (_, _) => await AutoRefreshAsync();
        Loaded += async (_, _) =>
        {
            _autoRefreshTimer.Start();
            await LoadLatestLogAsync(force: true);
        };
        Closed += (_, _) => _autoRefreshTimer.Stop();
    }

    private async Task LoadLatestLogAsync(bool force)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var snapshot = await _logFileService.LoadLatestAsync();
            ApplySnapshot(snapshot, force || ShouldApplySnapshot(snapshot));
        }
        catch (Exception ex)
        {
            LogStatusText.Text = AppConstants.LogViewerLoadFailedText;
            DiagnosticLogService.WriteException("LogViewer", "Load latest log failed.", ex);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task AutoRefreshAsync()
    {
        if (AutoRefreshCheckBox.IsChecked != true || LogContentTextBox.SelectionLength > 0)
        {
            return;
        }

        await LoadLatestLogAsync(force: false);
    }

    private bool ShouldApplySnapshot(LogFileSnapshot snapshot)
    {
        if (_currentSnapshot is null)
        {
            return true;
        }

        return !string.Equals(_currentSnapshot.FilePath, snapshot.FilePath, StringComparison.OrdinalIgnoreCase)
            || _currentSnapshot.LastWriteTime != snapshot.LastWriteTime
            || _currentSnapshot.FileSizeBytes != snapshot.FileSizeBytes;
    }

    private void ApplySnapshot(LogFileSnapshot snapshot, bool shouldUpdateText)
    {
        _currentSnapshot = snapshot;
        LogFileNameText.Text = snapshot.HasLogFile
            ? snapshot.FileName
            : AppConstants.LogViewerNoLogFileText;
        LogMetadataText.Text = CreateMetadataText(snapshot);

        if (!shouldUpdateText)
        {
            return;
        }

        var selectionStart = LogContentTextBox.SelectionStart;
        var selectionLength = LogContentTextBox.SelectionLength;
        LogContentTextBox.Text = snapshot.Content;
        _lastSearchIndex = -1;

        if (selectionLength > 0 && selectionStart + selectionLength <= LogContentTextBox.Text.Length)
        {
            LogContentTextBox.Select(selectionStart, selectionLength);
        }
        else if (ScrollToEndCheckBox.IsChecked == true)
        {
            LogContentTextBox.ScrollToEnd();
        }

        LogStatusText.Text = snapshot.IsTruncated
            ? AppConstants.LogViewerTruncatedText
            : AppConstants.LogViewerLoadedText;
    }

    private static string CreateMetadataText(LogFileSnapshot snapshot)
    {
        if (!snapshot.HasLogFile)
        {
            return AppConstants.LogViewerNoLogFileText;
        }

        var updated = snapshot.LastWriteTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        return $"{snapshot.FolderPath} / {updated} / {snapshot.FileSizeBytes:N0} bytes";
    }

    private void CopyAllLog()
    {
        if (string.IsNullOrWhiteSpace(LogContentTextBox.Text)
            || LogContentTextBox.Text == AppConstants.LogViewerNoLogFileText)
        {
            LogStatusText.Text = AppConstants.LogViewerCopyEmptyText;
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(LogContentTextBox.Text);
            LogStatusText.Text = AppConstants.LogViewerCopySucceededText;
        }
        catch (Exception ex)
        {
            LogStatusText.Text = AppConstants.LogViewerCopyFailedText;
            DiagnosticLogService.WriteException("LogViewer", "Copy all log failed.", ex);
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            _logFileService.OpenLogsFolder();
            LogStatusText.Text = AppConstants.LogViewerOpenFolderSucceededText;
        }
        catch (Exception)
        {
            LogStatusText.Text = AppConstants.LogViewerOpenFolderFailedText;
        }
    }

    private void FindNext()
    {
        var query = SearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            LogStatusText.Text = AppConstants.LogViewerSearchEmptyText;
            return;
        }

        var text = LogContentTextBox.Text;
        if (string.IsNullOrEmpty(text))
        {
            LogStatusText.Text = AppConstants.LogViewerSearchNotFoundText;
            return;
        }

        var start = Math.Max(0, LogContentTextBox.SelectionStart + LogContentTextBox.SelectionLength);
        if (_lastSearchIndex >= 0 && start <= _lastSearchIndex)
        {
            start = _lastSearchIndex + query.Length;
        }

        var index = text.IndexOf(query, Math.Min(start, text.Length), StringComparison.OrdinalIgnoreCase);
        if (index < 0 && start > 0)
        {
            index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        }

        if (index < 0)
        {
            LogStatusText.Text = AppConstants.LogViewerSearchNotFoundText;
            return;
        }

        _lastSearchIndex = index;
        LogContentTextBox.Focus();
        LogContentTextBox.Select(index, query.Length);
        LogStatusText.Text = AppConstants.LogViewerSearchFoundText;
    }

    private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
    }

    private void LogWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
        }
    }
}
