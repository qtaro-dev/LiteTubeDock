namespace LiteTubeDock.Constants;

public static class IpcConstants
{
    public const string PipeNamePrefix = "LiteTubeDock_";
    public const string CommandPing = "ping";
    public const string CommandNavigate = "navigate";
    public const string CommandGetStatus = "get-status";
    public const string PongMessage = "pong";
    public const string NavigateAcceptedMessage = "navigate accepted";
    public const string StatusMessage = "status";
    public const string InvalidJsonMessage = "Invalid JSON.";
    public const string UnsupportedCommandMessage = "Unsupported command.";
    public const string InvalidUrlMessage = "Invalid URL.";
    public const string WebViewNotReadyMessage = "WebView2 is not ready.";
    public const string InternalErrorMessage = "Internal error.";
    public const string ErrorCodeInvalidJson = "invalid-json";
    public const string ErrorCodeUnsupportedCommand = "unsupported-command";
    public const string ErrorCodeInvalidUrl = "invalid-url";
    public const string ErrorCodeWebViewNotReady = "webview-not-ready";
    public const string ErrorCodeInternalError = "internal-error";
    public const int MaxCommandBytes = 16 * 1024;
    public const int MaxUrlLength = 4096;
    public const int WebViewReadyTimeoutMilliseconds = 5000;

    public static string GetPipeName(int processId) => PipeNamePrefix + processId;
}
