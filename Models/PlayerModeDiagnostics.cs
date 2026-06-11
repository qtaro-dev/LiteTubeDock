namespace LiteTubeDock.Models;

public sealed class PlayerModeDiagnostics
{
    public string LastOriginalUrl { get; private set; } = "-";

    public string LastPlayerUrl { get; private set; } = "-";

    public string LastFallbackUrl { get; private set; } = "-";

    public bool LastVideoIdExtracted { get; private set; }

    public string LastRefererRequestUrl { get; private set; } = "-";

    public string LastRefererResult { get; private set; } = "-";

    public string LastError { get; private set; } = "-";

    public int RefererAttachCount { get; private set; }

    public string SettingsApplyState { get; private set; } = "起動時設定を適用済み";

    public string SettingsAppliedAt { get; private set; } = "-";

    public void RecordSettingsApplied(DateTime appliedAt, bool isImmediate)
    {
        SettingsApplyState = isImmediate ? "即時反映済み" : "起動時設定を適用済み";
        SettingsAppliedAt = appliedAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void RecordPlayerUrl(string originalUrl, string navigationUrl, bool videoIdExtracted)
    {
        LastOriginalUrl = string.IsNullOrWhiteSpace(originalUrl) ? "-" : originalUrl;
        LastPlayerUrl = string.IsNullOrWhiteSpace(navigationUrl) ? "-" : navigationUrl;
        LastFallbackUrl = videoIdExtracted ? "-" : LastOriginalUrl;
        LastVideoIdExtracted = videoIdExtracted;
    }

    public void RecordRefererAttached(string requestUrl)
    {
        LastRefererRequestUrl = string.IsNullOrWhiteSpace(requestUrl) ? "-" : requestUrl;
        LastRefererResult = "付与成功";
        LastError = "-";
        RefererAttachCount++;
    }

    public void RecordRefererSkipped(string? requestUrl, string reason)
    {
        LastRefererRequestUrl = string.IsNullOrWhiteSpace(requestUrl) ? "-" : requestUrl;
        LastRefererResult = "スキップ: " + reason;
    }

    public void RecordRefererFailed(string? requestUrl, string errorMessage)
    {
        LastRefererRequestUrl = string.IsNullOrWhiteSpace(requestUrl) ? "-" : requestUrl;
        LastRefererResult = "失敗";
        LastError = string.IsNullOrWhiteSpace(errorMessage) ? "-" : errorMessage;
    }

    public string ToDisplayText(bool refererEnabled, string refererValue)
    {
        var refererState = refererEnabled ? "ON" : "OFF";
        var videoIdState = LastVideoIdExtracted ? "成功" : "失敗";

        return $"""
プレイヤーモード診断

Referer付与: {refererState}
Referer値: {refererValue}
設定反映状態: {SettingsApplyState}
設定反映時刻: {SettingsAppliedAt}
最後の元URL: {LastOriginalUrl}
最後のプレイヤーURL: {LastPlayerUrl}
最後のフォールバックURL: {LastFallbackUrl}
動画ID抽出: {videoIdState}
最後のReferer対象URL: {LastRefererRequestUrl}
最後のReferer結果: {LastRefererResult}
Referer付与回数: {RefererAttachCount}
最終エラー: {LastError}

プレイヤーモードは実験的機能です。再生できない場合は通常モードを使用してください。
""";
    }
}
