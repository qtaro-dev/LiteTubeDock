using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services;
using Forms = System.Windows.Forms;

namespace LiteTubeDock.Views;

public partial class GeneralSettingsView : System.Windows.Controls.UserControl
{
    private const string CurrentSizePresetLabel = "現在のサイズ";
    private const string CustomPresetLabel = AppConstants.CustomWindowSizePresetLabel;

    private bool _isLoadingValues;
    private double _currentWindowWidth = AppConstants.DefaultWindowWidth;
    private double _currentWindowHeight = AppConstants.DefaultWindowHeight;

    public bool WindowSizeChanged { get; private set; }

    public bool ResetWindowBoundsRequested { get; private set; }

    public GeneralSettingsView()
    {
        InitializeComponent();

        CaptureCurrentWindowSizeButton.Click += (_, _) => CaptureCurrentWindowSize();
        ResetWindowBoundsButton.Click += (_, _) => ResetWindowBoundsFormValues();
        WindowSizePresetComboBox.SelectionChanged += (_, _) => WindowSizePresetComboBox_SelectionChanged();
        WindowWidthTextBox.TextChanged += (_, _) => MarkWindowSizeAsCustom();
        WindowHeightTextBox.TextChanged += (_, _) => MarkWindowSizeAsCustom();
    }

    public void LoadSettings(AppSettings settings)
    {
        _isLoadingValues = true;

        HomeUrlTextBox.Text = settings.HomeUrl;
        RestoreLastUrlCheckBox.IsChecked = settings.RestoreLastUrl;
        StartAlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        RestoreWindowStateCheckBox.IsChecked = settings.RestoreWindowState;
        AutoplayCheckBox.IsChecked = settings.EnableAutoplay;
        ConfirmNonVideoNavigationCheckBox.IsChecked = settings.ConfirmNonVideoNavigation;
        PlayerModeRefererEnabledCheckBox.IsChecked = settings.PlayerModeRefererEnabled;
        PlayerModeRefererTextBox.Text = NormalizeReferer(settings.PlayerModeReferer);
        SettingsExportFolderTextBox.Text = AppConstants.SettingsDirectoryPath;
        WindowWidthTextBox.Text = settings.Window.Width.ToString("0");
        WindowHeightTextBox.Text = settings.Window.Height.ToString("0");
        SelectWindowSizePreset(settings.WindowSizePreset, settings.Window.Width, settings.Window.Height);

        WindowSizeChanged = false;
        _isLoadingValues = false;
    }

    public void SetCurrentWindowSize(double width, double height)
    {
        if (!double.IsNaN(width) && !double.IsInfinity(width) && width > 0)
        {
            _currentWindowWidth = Math.Round(width);
        }

        if (!double.IsNaN(height) && !double.IsInfinity(height) && height > 0)
        {
            _currentWindowHeight = Math.Round(height);
        }
    }

    public void CollectSettings(AppSettings settings)
    {
        settings.HomeUrl = NormalizeUrl(HomeUrlTextBox.Text, AppConstants.DefaultHomeUrl);
        settings.RestoreLastUrl = RestoreLastUrlCheckBox.IsChecked == true;
        settings.AlwaysOnTop = StartAlwaysOnTopCheckBox.IsChecked == true;
        settings.RestoreWindowState = RestoreWindowStateCheckBox.IsChecked == true;
        settings.EnableAutoplay = AutoplayCheckBox.IsChecked == true;
        settings.ConfirmNonVideoNavigation = ConfirmNonVideoNavigationCheckBox.IsChecked == true;
        settings.PlayerModeRefererEnabled = PlayerModeRefererEnabledCheckBox.IsChecked == true;
        settings.PlayerModeReferer = NormalizeReferer(PlayerModeRefererTextBox.Text);
        settings.SettingsExportFolder = AppConstants.SettingsDirectoryPath;
        settings.Window.Width = ParseWindowSize(WindowWidthTextBox.Text, settings.Window.Width, AppConstants.MinWindowWidth, AppConstants.MaxWindowWidth);
        settings.Window.Height = ParseWindowSize(WindowHeightTextBox.Text, settings.Window.Height, AppConstants.MinWindowHeight, AppConstants.MaxWindowHeight);
        settings.WindowSizePreset = GetSelectedWindowSizePreset(settings.Window.Width, settings.Window.Height);
    }

    public void ResetWindowBoundsFormValues()
    {
        ResetWindowBoundsRequested = true;
        WindowSizeChanged = true;

        _isLoadingValues = true;
        SelectComboBoxItem(AppConstants.DefaultWindowSizePreset);
        WindowWidthTextBox.Text = AppConstants.DefaultWindowWidth.ToString("0");
        WindowHeightTextBox.Text = AppConstants.DefaultWindowHeight.ToString("0");
        _isLoadingValues = false;
    }

    public void ClearResetWindowBoundsRequest()
    {
        ResetWindowBoundsRequested = false;
    }

    public void MarkWindowSizeChanged()
    {
        WindowSizeChanged = true;
    }

    public void ApplyDefaultWindowBounds(AppSettings settings)
    {
        if (!ResetWindowBoundsRequested)
        {
            return;
        }

        settings.Window.Left = AppConstants.DefaultWindowLeft;
        settings.Window.Top = AppConstants.DefaultWindowTop;
        settings.Window.Width = AppConstants.DefaultWindowWidth;
        settings.Window.Height = AppConstants.DefaultWindowHeight;
        settings.WindowSizePreset = AppConstants.DefaultWindowSizePreset;
    }

    private void CaptureCurrentWindowSize()
    {
        _isLoadingValues = true;
        WindowWidthTextBox.Text = _currentWindowWidth.ToString("0");
        WindowHeightTextBox.Text = _currentWindowHeight.ToString("0");
        SelectComboBoxItem(CurrentSizePresetLabel);
        WindowSizeChanged = true;
        _isLoadingValues = false;
    }

    private void BrowseSettingsExportFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "設定フォルダを選択",
            SelectedPath = GetSettingsExportFolderPath()
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SettingsExportFolderTextBox.Text = Path.GetFullPath(dialog.SelectedPath);
        }
    }

    private void OpenSettingsExportFolder()
    {
        try
        {
            var folder = GetSettingsExportFolderPath();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            System.Windows.MessageBox.Show(
                AppConstants.SettingsFolderOpenFailedMessage,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private string GetSettingsExportFolderPath()
    {
        return AppPathService.ResolveSettingsExportFolder(SettingsExportFolderTextBox.Text);
    }

    private static string NormalizeSettingsExportFolder(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? AppPathService.ResolveSettingsExportFolder(AppConstants.DefaultSettingsExportFolder)
            : AppPathService.ResolveSettingsExportFolder(value);
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

    private void WindowSizePresetComboBox_SelectionChanged()
    {
        if (_isLoadingValues)
        {
            return;
        }

        var preset = GetSelectedComboBoxText();
        WindowSizeChanged = true;
        if (preset == CurrentSizePresetLabel)
        {
            _isLoadingValues = true;
            WindowWidthTextBox.Text = _currentWindowWidth.ToString("0");
            WindowHeightTextBox.Text = _currentWindowHeight.ToString("0");
            _isLoadingValues = false;
            return;
        }

        if (AppConstants.TryGetWindowSizePreset(preset, out var width, out var height))
        {
            _isLoadingValues = true;
            WindowWidthTextBox.Text = width.ToString("0");
            WindowHeightTextBox.Text = height.ToString("0");
            _isLoadingValues = false;
        }
    }

    private void MarkWindowSizeAsCustom()
    {
        if (_isLoadingValues)
        {
            return;
        }

        WindowSizeChanged = true;

        if (!AppConstants.TryGetWindowSizePreset(GetSelectedComboBoxText(), out var presetWidth, out var presetHeight)
            || !double.TryParse(WindowWidthTextBox.Text, out var width)
            || !double.TryParse(WindowHeightTextBox.Text, out var height)
            || Math.Round(width) != presetWidth
            || Math.Round(height) != presetHeight)
        {
            SelectComboBoxItem(CustomPresetLabel);
        }
    }

    private void SelectWindowSizePreset(string preset, double width, double height)
    {
        if (AppConstants.TryGetWindowSizePreset(preset, out _, out _))
        {
            SelectComboBoxItem(preset);
            return;
        }

        SelectComboBoxItem(CustomPresetLabel);
    }

    private string GetSelectedWindowSizePreset(double width, double height)
    {
        var selected = GetSelectedComboBoxText();
        if (AppConstants.TryGetWindowSizePreset(selected, out _, out _))
        {
            return selected;
        }

        return AppConstants.CustomWindowSizePreset;
    }

    private string GetSelectedComboBoxText()
    {
        return (WindowSizePresetComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? CurrentSizePresetLabel;
    }

    private void SelectComboBoxItem(string text)
    {
        foreach (var item in WindowSizePresetComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content?.ToString() == text)
            {
                WindowSizePresetComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static double ParseWindowSize(string? value, double fallback, double min, double max)
    {
        if (!double.TryParse(value, out var parsed))
        {
            parsed = fallback;
        }

        if (double.IsNaN(parsed) || double.IsInfinity(parsed))
        {
            parsed = fallback;
        }

        return Math.Min(Math.Max(parsed, min), max);
    }
}
