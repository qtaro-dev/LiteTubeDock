namespace LiteTubeDock.Constants;

public static class AppConstants
{
    public const string AppName = "LiteTube Dock";
    public const string AppVersion = "0.2.1";
    public const string HelpWindowTitle = "LiteTube Dock ヘルプ";
    public const string AboutWindowTitle = "LiteTube Dock バージョン情報";
    public const string LogViewerWindowTitle = "LiteTube Dock ログ";
    public const string PlayerModeDiagnosticsTitle = "プレイヤーモード診断";
    public const string VersionPrefix = "Version ";
    public const string TechnologyHeadingText = "使用技術";
    public const string DeveloperHeadingText = "開発者";
    public const string DeveloperText = "Qtaro";
    public const string DeveloperDisplayText = "開発者: Qtaro";
    public const string DevelopmentSupportHeadingText = "開発支援";
    public const string DevelopmentSupportText = "GPT / CODEX";
    public const string DevelopmentSupportDisplayText = "開発支援: GPT / CODEX";
    public const string SecurityPolicyHeadingText = "セキュリティ方針";
    public const string CloseButtonText = "閉じる";
    public const string DataDirectoryName = "data";
    public const string LogsDirectoryName = "logs";
    public const string LogFilePrefix = "litetubedock_";
    public const int LogRetentionDays = 14;
    public const string SettingsExportDirectoryName = "settings";
    public const string IconsDirectoryName = "icons";
    public const string SettingsFileName = "settings.json";
    public const string BookmarksFileName = "bookmarks.json";
    public const string ProjectFileName = "LiteTubeDock.csproj";
    public const string DefaultWebView2UserDataFolder = "data/webview2-user-data";
    public const string DefaultSettingsExportFolder = "settings";
    public const string DefaultBookmarksExportFileName = "bookmarks_export.json";

    public static string AppBaseDirectory => System.AppContext.BaseDirectory;

    public static string DataDirectoryPath =>
        System.IO.Path.Combine(AppBaseDirectory, DataDirectoryName);

    public static string LogsDirectoryPath =>
        System.IO.Path.Combine(AppBaseDirectory, LogsDirectoryName);

    public static string SettingsDirectoryPath =>
        System.IO.Path.Combine(DataDirectoryPath, SettingsExportDirectoryName);

    public static string SettingsFilePath =>
        System.IO.Path.Combine(SettingsDirectoryPath, SettingsFileName);

    public static string BookmarksFilePath =>
        System.IO.Path.Combine(SettingsDirectoryPath, BookmarksFileName);

    public static string BookmarksExportFilePath =>
        System.IO.Path.Combine(SettingsDirectoryPath, DefaultBookmarksExportFileName);

    public static string WebView2UserDataFolderPath =>
        System.IO.Path.Combine(DataDirectoryPath, "webview2-user-data");

    public const string AutoplayBrowserArgument = "--autoplay-policy=no-user-gesture-required";
    public const string DefaultHomeUrl = "https://www.youtube.com/";
    public const string DefaultHomeBookmarkLabel = "Home";
    public const string DefaultBookmarkLabelPrefix = "URL";
    public const string DefaultBookmarkBackgroundColor = "#F0F0F0";
    public const string DefaultBookmarkForegroundColor = "#000000";
    public const int FavoriteButtonDisplayLabelMaxLength = 100;
    public const string FavoritePlaybackRangeNotFoundMessage = "再生箇所が見つかりません。ボタンの設定を見直してください。";
    public const string FavoritePlaybackPositionUnknownMessage = "再生位置を確認できませんでした。時間をおいて再度お試しください。";
    public const string RefererHeaderName = "Referer";
    public const string DefaultPlayerModeRefererValue = "https://litetubedock.local/";
    public const string DefaultWindowSizePreset = "800x600";
    public const string CustomWindowSizePreset = "Custom";
    public const string CustomWindowSizePresetLabel = "カスタム";
    public const string WindowSize800x600Preset = "800x600";
    public const string WindowSize960x540Preset = "960x540";
    public const string WindowSize1024x768Preset = "1024x768";
    public const string WindowSize1280x720Preset = "1280x720";
    public const string WindowSize1600x900Preset = "1600x900";
    public const string WindowSize1920x1080Preset = "1920x1080";
    public const string WindowSize540x960Preset = "540x960";
    public const string WindowSize720x1280Preset = "720x1280";
    public const string WindowSize768x1024Preset = "768x1024";
    public const string WindowSize900x1600Preset = "900x1600";
    public const string WindowSize1080x1920Preset = "1080x1920";
    public const double MinWindowWidth = 320;
    public const double MinWindowHeight = 240;
    public const double MaxWindowWidth = 3840;
    public const double MaxWindowHeight = 2160;
    public const double DefaultWindowLeft = 100;
    public const double DefaultWindowTop = 100;
    public const double DefaultWindowWidth = 800;
    public const double DefaultWindowHeight = 600;
    public const int MaxBookmarks = 10;

    public const string CurrentUrlPrefix = "現在URL: ";
    public const string CurrentUrlEmpty = "現在URL: -";
    public const string LoadingIdleText = "状態: 待機中";
    public const string LoadingStartingText = "状態: 読み込み中";
    public const string LoadingCompletedText = "状態: 完了";
    public const string LoadingFailedText = "状態: 失敗";
    public const string AddressBarEmptyText = "状態: URLを入力してください";
    public const string AddressBarInvalidText = "状態: URLが正しくありません";
    public const string CurrentUrlCopiedText = "状態: 現在URLをコピーしました";
    public const string CurrentUrlCopyFailedText = "状態: 現在URLをコピーできませんでした";
    public const string SettingsReloadedText = "状態: 設定を再読み込みしました";
    public const string SettingsSavedAppliedText = "状態: 設定を保存しました。変更を反映しました";
    public const string SettingsSavedRefererAppliedText = "状態: 設定を保存しました。プレイヤーモードReferer設定を反映しました";
    public const string BookmarksImportedText = "状態: お気に入り設定をインポートしました";
    public const string BookmarksExportedText = "状態: お気に入り設定をエクスポートしました";
    public const string BookmarksImportFailedText = "状態: お気に入り設定をインポートできませんでした";
    public const string BookmarksExportFailedText = "状態: お気に入り設定をエクスポートできませんでした";
    public const string BookmarksReloadedText = "状態: お気に入り設定を再読み込みしました";
    public const string BookmarksReloadFailedText = "状態: お気に入り設定を再読み込みできませんでした";
    public const string WebViewRefererSetupFailedText = "状態: YouTube埋め込み向けReferer設定を適用できませんでした";
    public const string WebViewRefererAttachedText = "状態: プレイヤーモードReferer付与を試行しました";
    public const string IpcStartFailedText = "状態: IPCの開始に失敗しました。ログを確認してください。";
    public const string LogViewerMenuText = "ログを表示";
    public const string LogMenuText = "ログ";
    public const string OpenLogFolderMenuText = "ログフォルダーを開く";
    public const string LogViewerReloadButtonText = "再読み込み";
    public const string LogViewerCopyAllButtonText = "全文コピー";
    public const string LogViewerOpenFolderButtonText = "ログフォルダーを開く";
    public const string LogViewerAutoRefreshText = "自動更新";
    public const string LogViewerScrollToEndText = "更新時に最下部へ移動";
    public const string LogViewerFindNextButtonText = "次を検索";
    public const string LogViewerSearchToolTip = "PID、Pipe名、Command、ErrorCode、MEDIA_NOT_FOUNDなどを検索できます。Ctrl+Fでも移動できます。";
    public const string LogViewerNoLogFileText = "表示できるログファイルがありません。";
    public const string LogViewerTruncatedText = "ログが大きいため、最新部分のみ表示しています。";
    public const string LogViewerLoadedText = "ログを読み込みました。";
    public const string LogViewerLoadFailedText = "ログを読み込めませんでした。";
    public const string LogViewerCopySucceededText = "ログ全文をコピーしました。";
    public const string LogViewerCopyEmptyText = "コピーできるログがありません。";
    public const string LogViewerCopyFailedText = "ログをコピーできませんでした。";
    public const string LogViewerOpenFolderSucceededText = "ログフォルダーを開きました。";
    public const string LogViewerOpenFolderFailedText = "ログフォルダーを開けませんでした。";
    public const string LogViewerSearchEmptyText = "検索文字列を入力してください。";
    public const string LogViewerSearchFoundText = "検索結果へ移動しました。";
    public const string LogViewerSearchNotFoundText = "該当する文字列がありません。";
    public const string WindowSizeChangedText = "状態: ウィンドウサイズを変更しました";
    public const string WindowPositionResetText = "状態: ウィンドウ位置をリセットしました";
    public const string WindowBoundsResetText = "状態: ウィンドウ位置とサイズをリセットしました";
    public const string FavoriteRegistrationFailedText = "状態: 現在URLを取得できませんでした";
    public const string DefaultFavoriteMovieLabel = "YouTube";
    public const int FavoriteMovieLabelMaxLength = 20;
    public const string FullScreenOnText = "状態: フルスクリーン";
    public const string FullScreenOffText = "状態: 通常表示";
    public const string PlayerModeChromeHiddenText = "状態: プレイヤーモードのタイトルバーを非表示にしました";
    public const string PlayerModeChromeHideFailedText = "状態: プレイヤーモードのタイトルバー非表示に失敗しました";
    public const string WindowStateLogCategory = "WindowState";
    public const string PlayerModeWindowChromeNoneText = "None";
    public const int PlayerModeResizeBorderThickness = 8;
    public const int PlayerModeDragAreaHeight = 6;
    public const string AlwaysOnTopOnText = "最前面: ON";
    public const string AlwaysOnTopOffText = "最前面: OFF";
    public const string EmptyBookmarkLabel = "-";
    public const string FavoriteDeleteButtonText = "削除";
    public const string FavoriteDeleteConfirmMessage = "このお気に入りを削除しますか？\n表示名、URL、アイコン、色、再生設定を初期値に戻します。";
    public const string BookmarksExternalChangedMessage = "外部でお気に入り設定が更新されました。\n再読み込みしますか？";
    public const string ImportBookmarksConfirmMessage = "現在のお気に入り設定を上書きします。インポートしてよろしいですか？";
    public const string SettingsFolderOpenFailedMessage = "設定フォルダを開けませんでした。";
    public const string NonVideoNavigationConfirmMessage = "動画ページ以外へ移動しようとしています。\n遷移してよろしいですか？";
    public const string NonVideoNavigationCancelledText = "状態: 動画ページ以外への遷移をキャンセルしました";
    public const string StartupArgumentHelpText = """
LiteTube Dock 起動引数

--player-mode
  動画表示領域を優先したプレイヤーモードで起動します。

--url "https://www.youtube.com/"
  起動時に指定URLを一時的に開きます。

--ipc-enabled
  LiteTubeDockControlとの連携用Named Pipe受信機能を有効にします。

--start-paused
  LiteTubeDockControl管理起動時、初回読み込み後にメディアを停止状態にします。

--help
  このヘルプを表示します。

使用例:
  LiteTubeDock.exe --player-mode --ipc-enabled --start-paused --url "https://www.youtube.com/"
""";

    public static bool TryGetWindowSizePreset(string preset, out double width, out double height)
    {
        (width, height) = preset switch
        {
            WindowSize800x600Preset => (800, 600),
            WindowSize960x540Preset => (960, 540),
            WindowSize1024x768Preset => (1024, 768),
            WindowSize1280x720Preset => (1280, 720),
            WindowSize1600x900Preset => (1600, 900),
            WindowSize1920x1080Preset => (1920, 1080),
            WindowSize540x960Preset => (540, 960),
            WindowSize720x1280Preset => (720, 1280),
            WindowSize768x1024Preset => (768, 1024),
            WindowSize900x1600Preset => (900, 1600),
            WindowSize1080x1920Preset => (1080, 1920),
            _ => (0, 0)
        };

        return width > 0 && height > 0;
    }

    public static string GetWindowSizePresetForSize(double width, double height)
    {
        return (Math.Round(width), Math.Round(height)) switch
        {
            (800, 600) => WindowSize800x600Preset,
            (960, 540) => WindowSize960x540Preset,
            (1024, 768) => WindowSize1024x768Preset,
            (1280, 720) => WindowSize1280x720Preset,
            (1600, 900) => WindowSize1600x900Preset,
            (1920, 1080) => WindowSize1920x1080Preset,
            (540, 960) => WindowSize540x960Preset,
            (720, 1280) => WindowSize720x1280Preset,
            (768, 1024) => WindowSize768x1024Preset,
            (900, 1600) => WindowSize900x1600Preset,
            (1080, 1920) => WindowSize1080x1920Preset,
            _ => CustomWindowSizePreset
        };
    }
}
