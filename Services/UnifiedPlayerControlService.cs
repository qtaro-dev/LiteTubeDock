using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using LiteTubeDock.Services.PlayerAdapters;
using Microsoft.Web.WebView2.Core;

namespace LiteTubeDock.Services;

public static class UnifiedPlayerControlService
{
    private const double SeekToleranceSeconds = 2.0;
    private const int SeekVerifyMaxAttempts = 8;
    private const int SeekVerifyRetryDelayMilliseconds = 250;
    private const int PlayVerifyMaxAttempts = 8;
    private const int PlayVerifyRetryDelayMilliseconds = 250;

    public const string OperationGetState = "get-state";
    public const string OperationPlay = "play";
    public const string OperationPause = "pause";
    public const string OperationStop = "stop";
    public const string OperationSeek = "seek";
    public const string OperationNext = "next";
    public const string OperationPrevious = "previous";
    public const string OperationNextChapter = "next-chapter";
    public const string OperationPreviousChapter = "previous-chapter";
    public const string OperationSetVolume = "set-volume";
    public const string OperationSetMuted = "set-muted";
    public const string OperationSetControlPolicy = "set-control-policy";
    public const string OperationClearControlPolicy = "clear-control-policy";
    public const string OperationToggleMute = "toggle-mute";
    public const string OperationSeekToStart = "seek-to-start";
    public const string OperationReapplyDesiredState = "reapply-desired-state";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IPlayerAdapter[] Adapters =
    [
        new YouTubePlayerAdapter(),
        new TwitchPlayerAdapter(),
        new GenericMediaPlayerAdapter()
    ];

    public static async Task<UnifiedPlayerStateResult> ExecuteAsync(
        CoreWebView2 webView,
        string operation,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        double? positionSeconds,
        int? volumePercent,
        bool? muted,
        UnifiedPlayerControlPolicy? controlPolicy,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteOnceAsync(
            webView,
            operation,
            desiredVolumePercent,
            desiredMutedState,
            positionSeconds,
            volumePercent,
            muted,
            controlPolicy,
            cancellationToken);

        if (operation is OperationSeek or OperationSeekToStart
            && result.MediaFound
            && result.RequestedPositionSeconds.HasValue
            && (string.Equals(result.OperationResult, "seeked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.OperationResult, "seek-requested", StringComparison.OrdinalIgnoreCase)))
        {
            return await VerifySeekAsync(
                webView,
                result,
                desiredVolumePercent,
                desiredMutedState,
                cancellationToken);
        }

        if (operation == OperationPlay
            && result.MediaFound
            && (string.Equals(result.OperationResult, "playing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.OperationResult, "play-requested", StringComparison.OrdinalIgnoreCase)))
        {
            return await VerifyPlayAsync(
                webView,
                result,
                desiredVolumePercent,
                desiredMutedState,
                cancellationToken);
        }

        return result;
    }

    private static async Task<UnifiedPlayerStateResult> ExecuteOnceAsync(
        CoreWebView2 webView,
        string operation,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        double? positionSeconds,
        int? volumePercent,
        bool? muted,
        UnifiedPlayerControlPolicy? controlPolicy,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var adapter = ResolveAdapter(webView.Source);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultJson = await webView.ExecuteScriptAsync(CreateScript(
                    operation,
                    adapter.SiteType,
                    adapter.PlayerType,
                    desiredVolumePercent,
                    desiredMutedState,
                    positionSeconds,
                    volumePercent,
                    muted,
                    controlPolicy))
                .WaitAsync(cancellationToken);
            var payload = DecodeScriptResult(resultJson, operation, adapter);
            var result = payload is null
                ? new UnifiedPlayerStateResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = "Player control response was empty.",
                    SiteType = adapter.SiteType,
                    PlayerType = adapter.PlayerType,
                    Operation = operation,
                    OperationResult = "empty-script-result",
                    OperationError = "empty-script-result"
                }
                : JsonSerializer.Deserialize<UnifiedPlayerStateResult>(payload, JsonOptions)
                    ?? new UnifiedPlayerStateResult
                    {
                        Success = false,
                        MediaFound = false,
                        ErrorCode = IpcConstants.ErrorCodeUnknownError,
                        Message = "Player control response was empty.",
                        SiteType = adapter.SiteType,
                        PlayerType = adapter.PlayerType,
                        Operation = operation,
                        OperationResult = "empty-script-result",
                        OperationError = "empty-script-result"
                    };

            return ApplyDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            return ApplyDuration(Failed(operation, adapter, IpcConstants.ErrorCodeTimeout, ex.Message, ex.Message), (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unified player control failed: {ex.Message}");
            return ApplyDuration(Failed(operation, adapter, IpcConstants.ErrorCodeScriptExecutionFailed, ex.Message, ex.Message), (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<UnifiedPlayerStateResult> VerifySeekAsync(
        CoreWebView2 webView,
        UnifiedPlayerStateResult initialResult,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        var requestedPosition = initialResult.RequestedPositionSeconds;
        if (requestedPosition.HasValue && IsSeekVerified(initialResult, requestedPosition.Value))
        {
            return WithOperationOutcome(
                initialResult,
                success: true,
                errorCode: null,
                message: "Seek completed.",
                operationResult: "seeked-verified",
                operationError: null,
                requestedPositionSeconds: requestedPosition,
                requestedVolumePercent: initialResult.RequestedVolumePercent,
                requestedMuted: initialResult.RequestedMuted);
        }

        UnifiedPlayerStateResult lastState = initialResult;
        for (var attempt = 1; attempt <= SeekVerifyMaxAttempts; attempt++)
        {
            await Task.Delay(SeekVerifyRetryDelayMilliseconds, cancellationToken);
            lastState = await ExecuteOnceAsync(
                webView,
                OperationGetState,
                desiredVolumePercent,
                desiredMutedState,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);

            if (requestedPosition.HasValue && IsSeekVerified(lastState, requestedPosition.Value))
            {
                return WithOperationOutcome(
                    lastState,
                    success: true,
                    errorCode: null,
                    message: "Seek completed.",
                    operationResult: "seeked-verified",
                    operationError: null,
                    requestedPositionSeconds: requestedPosition,
                    requestedVolumePercent: initialResult.RequestedVolumePercent,
                    requestedMuted: initialResult.RequestedMuted);
            }
        }

        return WithOperationOutcome(
            lastState,
            success: false,
            errorCode: IpcConstants.ErrorCodeTimeout,
            message: "Seek did not reach the requested position.",
            operationResult: "seek-verification-failed",
            operationError: "Requested position was not reached within tolerance.",
            requestedPositionSeconds: requestedPosition,
            requestedVolumePercent: initialResult.RequestedVolumePercent,
            requestedMuted: initialResult.RequestedMuted);
    }

    private static async Task<UnifiedPlayerStateResult> VerifyPlayAsync(
        CoreWebView2 webView,
        UnifiedPlayerStateResult initialResult,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        if (IsPlayingVerified(initialResult))
        {
            return WithOperationOutcome(
                initialResult,
                success: true,
                errorCode: null,
                message: "Playback started.",
                operationResult: "playing-verified",
                operationError: null,
                requestedPositionSeconds: initialResult.RequestedPositionSeconds,
                requestedVolumePercent: initialResult.RequestedVolumePercent,
                requestedMuted: initialResult.RequestedMuted);
        }

        UnifiedPlayerStateResult lastState = initialResult;
        for (var attempt = 1; attempt <= PlayVerifyMaxAttempts; attempt++)
        {
            await Task.Delay(PlayVerifyRetryDelayMilliseconds, cancellationToken);
            lastState = await ExecuteOnceAsync(
                webView,
                OperationGetState,
                desiredVolumePercent,
                desiredMutedState,
                positionSeconds: null,
                volumePercent: null,
                muted: null,
                controlPolicy: null,
                cancellationToken);

            if (IsPlayingVerified(lastState))
            {
                return WithOperationOutcome(
                    lastState,
                    success: true,
                    errorCode: null,
                    message: "Playback started.",
                    operationResult: "playing-verified",
                    operationError: null,
                    requestedPositionSeconds: initialResult.RequestedPositionSeconds,
                    requestedVolumePercent: initialResult.RequestedVolumePercent,
                    requestedMuted: initialResult.RequestedMuted);
            }
        }

        return WithOperationOutcome(
            lastState,
            success: false,
            errorCode: IpcConstants.ErrorCodePlayVerificationFailed,
            message: "Playback did not start.",
            operationResult: "play-verification-failed",
            operationError: "Media remained paused after play was requested.",
            requestedPositionSeconds: initialResult.RequestedPositionSeconds,
            requestedVolumePercent: initialResult.RequestedVolumePercent,
            requestedMuted: initialResult.RequestedMuted);
    }

    private static bool IsSeekVerified(UnifiedPlayerStateResult result, double requestedPositionSeconds)
    {
        return result.Success
            && result.MediaFound
            && result.CurrentTimeSeconds.HasValue
            && Math.Abs(result.CurrentTimeSeconds.Value - requestedPositionSeconds) <= SeekToleranceSeconds;
    }

    private static bool IsPlayingVerified(UnifiedPlayerStateResult result)
    {
        return result.Success
            && result.MediaFound
            && result.IsPaused == false
            && result.IsPlaying == true;
    }

    private static string? DecodeScriptResult(string resultJson, string operation, IPlayerAdapter adapter)
    {
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Object when !root.EnumerateObject().Any() => LogEmptyScriptResult(resultJson, operation, adapter),
                JsonValueKind.Object => root.GetRawText(),
                _ => root.GetRawText()
            };
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Unified player script result decode failed. TargetType={nameof(UnifiedPlayerStateResult)}; Exception={ex.Message}; Raw={resultJson}");
            DiagnosticLogService.Write(
                "IPC",
                "Event=ScriptResultDecode"
                + "; Service=UnifiedPlayerControl"
                + "; Command=" + operation
                + "; TargetType=" + nameof(UnifiedPlayerStateResult)
                + "; RawKind=invalid-json"
                + "; DecodePath=parse-json-root"
                + "; SiteType=" + adapter.SiteType
                + "; PlayerType=" + adapter.PlayerType
                + "; ExceptionType=" + ex.GetType().Name
                + "; Message=" + ex.Message
                + "; Raw=" + resultJson);
            return resultJson;
        }
    }

    private static string? LogEmptyScriptResult(string resultJson, string operation, IPlayerAdapter adapter)
    {
        DiagnosticLogService.Write(
            "IPC",
            "Event=ScriptResultDecode"
            + "; Service=UnifiedPlayerControl"
            + "; Command=" + operation
            + "; TargetType=" + nameof(UnifiedPlayerStateResult)
            + "; RawKind=object"
            + "; DecodePath=empty-object"
            + "; Reason=empty-script-result"
            + "; SiteType=" + adapter.SiteType
            + "; PlayerType=" + adapter.PlayerType
            + "; Raw=" + resultJson);
        return null;
    }

    private static IPlayerAdapter ResolveAdapter(string? source)
    {
        Uri.TryCreate(source, UriKind.Absolute, out var uri);
        return Adapters.First(adapter => adapter.CanHandle(uri));
    }

    private static UnifiedPlayerStateResult Failed(
        string operation,
        IPlayerAdapter adapter,
        string errorCode,
        string message,
        string? operationError)
    {
        return new UnifiedPlayerStateResult
        {
            Success = false,
            MediaFound = false,
            ErrorCode = errorCode,
            Message = message,
            SiteType = adapter.SiteType,
            PlayerType = adapter.PlayerType,
            Operation = operation,
            OperationError = operationError
        };
    }

    private static UnifiedPlayerStateResult ApplyDuration(UnifiedPlayerStateResult result, int durationMs)
    {
        return new UnifiedPlayerStateResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            SiteType = result.SiteType,
            PlayerType = result.PlayerType,
            CurrentUrl = result.CurrentUrl,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            Title = result.Title,
            CurrentTimeSeconds = result.CurrentTimeSeconds,
            DurationSeconds = result.DurationSeconds,
            IsPlaying = result.IsPlaying,
            IsPaused = result.IsPaused,
            IsEnded = result.IsEnded,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            IsAdvertisement = result.IsAdvertisement,
            SeekableRangeCount = result.SeekableRangeCount,
            SeekableStartSeconds = result.SeekableStartSeconds,
            SeekableEndSeconds = result.SeekableEndSeconds,
            VolumePercent = result.VolumePercent,
            IsMuted = result.IsMuted,
            DesiredVolumePercent = result.DesiredVolumePercent,
            DesiredMutedState = result.DesiredMutedState,
            ControlPolicyEnabled = result.ControlPolicyEnabled,
            ControlPolicyExpiresInSeconds = result.ControlPolicyExpiresInSeconds,
            CanGoNext = result.CanGoNext,
            CanGoPrevious = result.CanGoPrevious,
            CanGoNextChapter = result.CanGoNextChapter,
            CanGoPreviousChapter = result.CanGoPreviousChapter,
            CurrentChapter = result.CurrentChapter,
            ChapterCount = result.ChapterCount,
            EndedReason = result.EndedReason,
            Operation = result.Operation,
            OperationResult = result.OperationResult,
            RequestedPositionSeconds = result.RequestedPositionSeconds,
            RequestedVolumePercent = result.RequestedVolumePercent,
            RequestedMuted = result.RequestedMuted,
            MediaElementCount = result.MediaElementCount,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            OperationError = result.OperationError,
            DurationMs = durationMs
        };
    }

    private static UnifiedPlayerStateResult WithOperationOutcome(
        UnifiedPlayerStateResult result,
        bool success,
        string? errorCode,
        string message,
        string operationResult,
        string? operationError,
        double? requestedPositionSeconds,
        int? requestedVolumePercent,
        bool? requestedMuted)
    {
        return new UnifiedPlayerStateResult
        {
            Success = success,
            MediaFound = result.MediaFound,
            ErrorCode = errorCode,
            Message = message,
            SiteType = result.SiteType,
            PlayerType = result.PlayerType,
            CurrentUrl = result.CurrentUrl,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            Title = result.Title,
            CurrentTimeSeconds = result.CurrentTimeSeconds,
            DurationSeconds = result.DurationSeconds,
            IsPlaying = result.IsPlaying,
            IsPaused = result.IsPaused,
            IsEnded = result.IsEnded,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            IsAdvertisement = result.IsAdvertisement,
            SeekableRangeCount = result.SeekableRangeCount,
            SeekableStartSeconds = result.SeekableStartSeconds,
            SeekableEndSeconds = result.SeekableEndSeconds,
            VolumePercent = result.VolumePercent,
            IsMuted = result.IsMuted,
            DesiredVolumePercent = result.DesiredVolumePercent,
            DesiredMutedState = result.DesiredMutedState,
            ControlPolicyEnabled = result.ControlPolicyEnabled,
            ControlPolicyExpiresInSeconds = result.ControlPolicyExpiresInSeconds,
            CanGoNext = result.CanGoNext,
            CanGoPrevious = result.CanGoPrevious,
            CanGoNextChapter = result.CanGoNextChapter,
            CanGoPreviousChapter = result.CanGoPreviousChapter,
            CurrentChapter = result.CurrentChapter,
            ChapterCount = result.ChapterCount,
            EndedReason = result.EndedReason,
            Operation = operationResult.StartsWith("seek", StringComparison.OrdinalIgnoreCase)
                ? OperationSeek
                : operationResult.StartsWith("play", StringComparison.OrdinalIgnoreCase)
                    ? OperationPlay
                    : result.Operation,
            OperationResult = operationResult,
            RequestedPositionSeconds = requestedPositionSeconds,
            RequestedVolumePercent = requestedVolumePercent,
            RequestedMuted = requestedMuted,
            MediaElementCount = result.MediaElementCount,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            OperationError = operationError,
            DurationMs = result.DurationMs
        };
    }

    private static string CreateScript(
        string operation,
        string siteType,
        string playerType,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        double? positionSeconds,
        int? volumePercent,
        bool? muted,
        UnifiedPlayerControlPolicy? controlPolicy)
    {
        var operationJson = JsonSerializer.Serialize(operation);
        var siteTypeJson = JsonSerializer.Serialize(siteType);
        var playerTypeJson = JsonSerializer.Serialize(playerType);
        var desiredVolumeLiteral = desiredVolumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var desiredMutedLiteral = desiredMutedState.HasValue ? desiredMutedState.Value.ToString().ToLowerInvariant() : "null";
        var positionLiteral = positionSeconds.HasValue ? positionSeconds.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "null";
        var volumeLiteral = volumePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var mutedLiteral = muted.HasValue ? muted.Value.ToString().ToLowerInvariant() : "null";
        var controlPolicyJson = JsonSerializer.Serialize(controlPolicy);

        return $$"""
(() => {
  const operation = {{operationJson}};
  const siteType = {{siteTypeJson}};
  const playerType = {{playerTypeJson}};
  const desiredVolumePercentFromHost = {{desiredVolumeLiteral}};
  const desiredMutedFromHost = {{desiredMutedLiteral}};
  const requestedPositionSeconds = {{positionLiteral}};
  const requestedVolumePercent = {{volumeLiteral}};
  const requestedMuted = {{mutedLiteral}};
  const requestedControlPolicy = {{controlPolicyJson}};
  const finite = (value) => Number.isFinite(value) ? value : null;
  const percent = (value) => Number.isFinite(value) ? Math.round(value * 100) : null;
  const clampPercent = (value) => Number.isFinite(value) ? Math.max(0, Math.min(100, Math.round(value))) : null;
  const hash = (value) => {
    let h = 2166136261;
    for (let i = 0; i < value.length; i++) {
      h ^= value.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return (h >>> 0).toString(16);
  };
  const state = window.__ltdUnifiedPlayerControl || {
    identity: "",
    revision: 0,
    desiredVolumePercent: null,
    desiredMutedState: null,
    applying: false,
    observed: new WeakSet(),
    observer: null,
    reapplyTimer: 0,
    lastPlayError: "",
    lastApplyError: "",
    controlPolicy: {
      enabled: false,
      desiredVolumePercent: null,
      desiredMutedState: null,
      lastHeartbeatAt: 0,
      expirationSeconds: 0
    }
  };
  window.__ltdUnifiedPlayerControl = state;

  const allMedia = () => Array.from(document.querySelectorAll("video,audio"));
  const area = (element) => {
    const rect = element.getBoundingClientRect();
    return Math.max(0, rect.width) * Math.max(0, rect.height);
  };
  const score = (element) => {
    const style = window.getComputedStyle(element);
    const visible = style.display !== "none" && style.visibility !== "hidden" && area(element) > 0;
    const hasSource = Boolean(element.currentSrc || element.src);
    const hasDuration = Number.isFinite(element.duration) && element.duration > 0;
    let value = 0;
    if (element.tagName.toLowerCase() === "video") value += 1000;
    if (!element.paused) value += 800;
    if (visible) value += 600;
    if (element.readyState >= 2) value += 300;
    if (hasSource) value += 200;
    if (hasDuration) value += 100;
    value += Math.min(area(element), 100000) / 1000;
    value += Number.isFinite(element.__ltdLastPlayAt) ? Math.max(0, Math.min(500, 500 - (Date.now() - element.__ltdLastPlayAt))) : 0;
    if (!visible) value -= 500;
    if (element.readyState === 0 && !hasSource) value -= 400;
    return value;
  };
  const targetMedia = () => allMedia()
    .filter((element) => typeof element.play === "function")
    .sort((a, b) => score(b) - score(a))[0] || null;
  const identityOf = (target) => {
    if (!target) return "";
    const duration = Number.isFinite(target.duration) ? target.duration.toFixed(3) : "live";
    return hash([
      target.currentSrc || target.src || "",
      location.href || "",
      document.title || "",
      duration,
      target.tagName.toLowerCase()
    ].join("|"));
  };
  const policyActive = () => {
    if (!state.controlPolicy.enabled) return false;
    const elapsed = (Date.now() - state.controlPolicy.lastHeartbeatAt) / 1000;
    if (state.controlPolicy.expirationSeconds > 0 && elapsed > state.controlPolicy.expirationSeconds) {
      state.controlPolicy.enabled = false;
      return false;
    }
    return true;
  };
  const syncDesiredFromHost = () => {
    if (desiredVolumePercentFromHost !== null) state.desiredVolumePercent = clampPercent(desiredVolumePercentFromHost);
    if (desiredMutedFromHost !== null) state.desiredMutedState = Boolean(desiredMutedFromHost);
  };
  const activeDesiredVolume = () => policyActive()
    ? state.controlPolicy.desiredVolumePercent
    : state.desiredVolumePercent;
  const activeDesiredMuted = () => policyActive()
    ? state.controlPolicy.desiredMutedState
    : state.desiredMutedState;
  const operationErrorForResult = (result) => {
    if (result === "invalid-volume") return "volumePercent is required and must be between 0 and 100.";
    if (result === "invalid-muted") return "muted is required and must be true or false.";
    if (result === "volume-mismatch") return "Requested volume was not applied.";
    if (result === "muted-mismatch") return "Requested muted state was not applied.";
    if (result === "desired-apply-failed") return state.lastApplyError || "Desired player state could not be applied.";
    if (result === "seek-out-of-range") return "Requested seek position is outside the playable range.";
    if (result === "seek-not-supported") return "Media is not seekable.";
    if (result === "advertisement-active") return "Advertisement is currently active.";
    if (result === "play-rejected") return state.lastPlayError || "play() was rejected.";
    if (result === "play-requested") return "Playback request has not been verified yet.";
    if (result === "seek-requested") return "Seek request has not been verified yet.";
    return "";
  };
  const validateSeekPosition = (target, position) => {
    if (!Number.isFinite(position) || position < 0) return { ok: false, result: "invalid-position" };
    const isAdvertisement = siteType === "youtube"
      && Boolean(document.querySelector(".html5-video-player.ad-showing, .ytp-ad-player-overlay, .ytp-ad-module, .ytp-ad-text, .ytp-ad-skip-button"));
    if (isAdvertisement) return { ok: false, result: "advertisement-active" };
    if (!target) return { ok: false, result: "media-not-found" };
    const seekableRangeCount = target.seekable ? target.seekable.length : 0;
    const isLive = !Number.isFinite(target.duration) || target.duration === Infinity;
    if (seekableRangeCount > 0) {
      const seekableStart = finite(target.seekable.start(0));
      const seekableEnd = finite(target.seekable.end(seekableRangeCount - 1));
      if (seekableStart === null || seekableEnd === null) return { ok: false, result: "seek-not-supported" };
      return position >= seekableStart && position <= seekableEnd
        ? { ok: true, result: "seekable-range" }
        : { ok: false, result: "seek-out-of-range" };
    }
    if (!isLive && Number.isFinite(target.duration) && target.duration > 0) {
      return position < target.duration
        ? { ok: true, result: "duration-range" }
        : { ok: false, result: "seek-out-of-range" };
    }
    return { ok: false, result: "seek-not-supported" };
  };
  const snapshot = (operationResult, operationError) => {
    const media = allMedia();
    const target = targetMedia();
    const identity = identityOf(target);
    if (identity && identity !== state.identity) {
      state.identity = identity;
      state.revision += 1;
    }
    const isLive = target ? (!Number.isFinite(target.duration) || target.duration === Infinity) : false;
    const duration = target ? finite(target.duration) : null;
    const currentTime = target ? finite(target.currentTime) : null;
    const seekableRangeCount = target && target.seekable ? target.seekable.length : 0;
    const seekableStart = seekableRangeCount > 0 ? finite(target.seekable.start(0)) : null;
    const seekableEnd = seekableRangeCount > 0 ? finite(target.seekable.end(seekableRangeCount - 1)) : null;
    const visibleAdElement = () => Array.from(document.querySelectorAll(
      ".ytp-ad-player-overlay, .ytp-ad-module, .ytp-ad-text, .ytp-ad-skip-button"
    )).some((element) => {
      const style = window.getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
    });
    const isAdvertisement = siteType === "youtube"
      && (Boolean(document.querySelector(".html5-video-player.ad-showing")) || visibleAdElement());
    const endedByTime = target && !isLive && duration !== null && currentTime !== null && duration > 0 && currentTime >= duration - 0.35;
    return {
      success: true,
      mediaFound: Boolean(target),
      errorCode: target ? null : "{{IpcConstants.ErrorCodeMediaNotFound}}",
      message: target ? "Player state was retrieved." : "Media element was not found.",
      siteType,
      playerType,
      currentUrl: location.href || "",
      mediaIdentity: identity,
      mediaRevision: state.revision,
      title: (document.title || "").replace(/\s*-\s*YouTube\s*$/i, ""),
      currentTimeSeconds: currentTime,
      durationSeconds: duration,
      isPlaying: target ? !target.paused : null,
      isPaused: target ? target.paused : null,
      isEnded: target ? Boolean(target.ended || endedByTime) : null,
      isSeekable: target ? (seekableRangeCount > 0 || (!isLive && Number.isFinite(target.duration))) : false,
      isLive,
      isAdvertisement,
      seekableRangeCount,
      seekableStartSeconds: seekableStart,
      seekableEndSeconds: seekableEnd,
      volumePercent: target ? percent(target.volume) : null,
      isMuted: target ? Boolean(target.muted) : null,
      desiredVolumePercent: activeDesiredVolume(),
      desiredMutedState: activeDesiredMuted(),
      controlPolicyEnabled: policyActive(),
      controlPolicyExpiresInSeconds: state.controlPolicy.enabled && state.controlPolicy.expirationSeconds > 0
        ? Math.max(0, Math.ceil(state.controlPolicy.expirationSeconds - ((Date.now() - state.controlPolicy.lastHeartbeatAt) / 1000)))
        : 0,
      canGoNext: Boolean(findNextButton()),
      canGoPrevious: Boolean(findPreviousButton()),
      canGoNextChapter: Boolean(findNextChapterButton()),
      canGoPreviousChapter: Boolean(findPreviousChapterButton()),
      currentChapter: "",
      chapterCount: 0,
      endedReason: target && target.ended ? "ended" : endedByTime ? "duration-reached" : "",
      operation,
      operationResult: operationResult || "",
      requestedPositionSeconds,
      requestedVolumePercent,
      requestedMuted,
      mediaElementCount: media.length,
      targetElementTag: target ? target.tagName.toLowerCase() : "",
      currentSrc: target ? (target.currentSrc || target.src || "") : "",
      readyState: target ? target.readyState : null,
      operationError: operationError || null
    };
  };
  const findButton = (selectors) => selectors
    .map((selector) => document.querySelector(selector))
    .find((element) => element && !element.disabled && element.offsetParent !== null) || null;
  function findNextButton() {
    return findButton([
      ".ytp-next-button",
      "button[data-a-target='player-next-button']",
      "[aria-label='Next']",
      "[aria-label='次へ']"
    ]);
  }
  function findPreviousButton() {
    return findButton([
      ".ytp-prev-button",
      "button[data-a-target='player-prev-button']",
      "[aria-label='Previous']",
      "[aria-label='前へ']"
    ]);
  }
  function findNextChapterButton() {
    return findButton([".ytp-chapter-next-button", "[aria-label*='Next chapter']", "[aria-label*='次のチャプター']"]);
  }
  function findPreviousChapterButton() {
    return findButton([".ytp-chapter-prev-button", "[aria-label*='Previous chapter']", "[aria-label*='前のチャプター']"]);
  }
  const applyDesired = (reason) => {
    const target = targetMedia();
    if (!target) return "media-not-found";
    const desiredVolume = activeDesiredVolume();
    const desiredMuted = activeDesiredMuted();
    if (desiredVolume === null && desiredMuted === null) return "no-desired-state";
    try {
      state.applying = true;
      if (desiredVolume !== null) target.volume = desiredVolume / 100;
      if (desiredMuted !== null) target.muted = Boolean(desiredMuted);
    } catch (error) {
      state.lastApplyError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : `${reason}: failed`;
      return "desired-apply-failed";
    } finally {
      state.applying = false;
    }
    state.lastApplyError = "";
    if (desiredVolume !== null && percent(target.volume) !== desiredVolume) return "volume-mismatch";
    if (desiredMuted !== null && Boolean(target.muted) !== Boolean(desiredMuted)) return "muted-mismatch";
    return "reapplied";
  };
  const scheduleApply = (reason) => {
    if (!policyActive() && activeDesiredVolume() === null && activeDesiredMuted() === null) return;
    window.clearTimeout(state.reapplyTimer);
    state.reapplyTimer = window.setTimeout(() => applyDesired(reason), 80);
  };
  const attachMedia = (element) => {
    if (state.observed.has(element)) return;
    state.observed.add(element);
    ["loadedmetadata", "canplay", "play", "playing", "emptied", "durationchange", "loadeddata", "ended"].forEach((eventName) => {
      element.addEventListener(eventName, () => {
        if (eventName === "play" || eventName === "playing") element.__ltdLastPlayAt = Date.now();
        scheduleApply(eventName);
      }, true);
    });
    element.addEventListener("volumechange", () => {
      if (state.applying) return;
      if (policyActive()) {
        scheduleApply("volumechange");
        return;
      }
      state.desiredVolumePercent = percent(element.volume);
      state.desiredMutedState = Boolean(element.muted);
    }, true);
  };
  const ensureObserver = () => {
    allMedia().forEach(attachMedia);
    if (state.observer) return;
    state.observer = new MutationObserver(() => {
      allMedia().forEach(attachMedia);
      scheduleApply("mutation");
    });
    state.observer.observe(document.documentElement || document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["src"]
    });
  };
  const clickAndConfirm = (button) => {
    if (!button) return "button-not-found";
    button.click();
    return "button-clicked";
  };

  ensureObserver();
  syncDesiredFromHost();
  let operationResult = "state";
  let operationError = "";
  const before = snapshot("", "");
  const target = targetMedia();

  try {
    if (operation === "set-control-policy") {
      const policyEnabled = requestedControlPolicy ? (requestedControlPolicy.enabled ?? requestedControlPolicy.Enabled) : false;
      const policyVolume = requestedControlPolicy ? (requestedControlPolicy.desiredVolumePercent ?? requestedControlPolicy.DesiredVolumePercent) : null;
      const policyMuted = requestedControlPolicy ? (requestedControlPolicy.desiredMutedState ?? requestedControlPolicy.DesiredMutedState) : null;
      const policyExpiration = requestedControlPolicy ? (requestedControlPolicy.expirationSeconds ?? requestedControlPolicy.ExpirationSeconds) : null;
      state.controlPolicy.enabled = Boolean(policyEnabled);
      state.controlPolicy.desiredVolumePercent = clampPercent(policyVolume);
      state.controlPolicy.desiredMutedState = policyMuted !== null && policyMuted !== undefined
        ? Boolean(policyMuted)
        : null;
      state.controlPolicy.expirationSeconds = Number.isFinite(policyExpiration)
        ? Math.max(1, Math.round(policyExpiration))
        : 30;
      state.controlPolicy.lastHeartbeatAt = Date.now();
      operationResult = applyDesired("set-control-policy");
    } else if (operation === "clear-control-policy") {
      state.controlPolicy.enabled = false;
      operationResult = "control-policy-cleared";
    } else if (!target) {
      operationResult = "media-not-found";
    } else if (operation === "reapply-desired-state") {
      operationResult = applyDesired("reapply-desired-state");
    } else if (operation === "play") {
      state.lastPlayError = "";
      applyDesired("before-play");
      try {
        const playResult = target.play();
        if (playResult && typeof playResult.catch === "function") {
          playResult.catch((error) => {
            state.lastPlayError = error && (error.name || error.message)
              ? `${error.name || "Error"}: ${error.message || ""}`
              : "play-failed";
          });
        }
        operationResult = target.paused ? "play-requested" : "playing";
      } catch (error) {
        state.lastPlayError = error && (error.name || error.message)
          ? `${error.name || "Error"}: ${error.message || ""}`
          : "play-failed";
        operationError = state.lastPlayError;
        operationResult = "play-rejected";
      }
    } else if (operation === "pause") {
      target.pause();
      operationResult = target.paused ? "paused" : "pause-requested";
    } else if (operation === "stop") {
      target.pause();
      if (Number.isFinite(target.currentTime)) target.currentTime = 0;
      operationResult = "stopped";
    } else if (operation === "seek" || operation === "seek-to-start") {
      const position = operation === "seek-to-start" ? 0 : requestedPositionSeconds;
      const seekValidation = validateSeekPosition(target, position);
      if (!seekValidation.ok) {
        operationResult = seekValidation.result;
        operationError = operationErrorForResult(operationResult);
      } else {
        target.currentTime = position;
        operationResult = Math.abs(target.currentTime - position) <= {{SeekToleranceSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}} ? "seeked" : "seek-requested";
      }
    } else if (operation === "set-volume") {
      const requestedVolume = clampPercent(requestedVolumePercent);
      if (requestedVolume === null || requestedVolumePercent !== requestedVolume) {
        operationResult = "invalid-volume";
        operationError = operationErrorForResult(operationResult);
      } else {
        state.desiredVolumePercent = requestedVolume;
        target.volume = state.desiredVolumePercent / 100;
        operationResult = percent(target.volume) === state.desiredVolumePercent ? "volume-set" : "volume-mismatch";
        operationError = operationErrorForResult(operationResult);
      }
    } else if (operation === "set-muted") {
      if (requestedMuted !== true && requestedMuted !== false) {
        operationResult = "invalid-muted";
        operationError = operationErrorForResult(operationResult);
      } else {
        state.desiredMutedState = requestedMuted;
        target.muted = state.desiredMutedState;
        operationResult = Boolean(target.muted) === state.desiredMutedState ? "muted-set" : "muted-mismatch";
        operationError = operationErrorForResult(operationResult);
      }
    } else if (operation === "toggle-mute") {
      target.muted = !target.muted;
      state.desiredMutedState = Boolean(target.muted);
      operationResult = "muted-toggled";
    } else if (operation === "next") {
      operationResult = clickAndConfirm(findNextButton());
    } else if (operation === "previous") {
      operationResult = clickAndConfirm(findPreviousButton());
    } else if (operation === "next-chapter") {
      operationResult = clickAndConfirm(findNextChapterButton());
    } else if (operation === "previous-chapter") {
      operationResult = clickAndConfirm(findPreviousChapterButton());
    } else {
      operationResult = "unsupported-operation";
    }
  } catch (error) {
    operationError = error && (error.name || error.message)
      ? `${error.name || "Error"}: ${error.message || ""}`
      : "Player operation failed.";
    operationResult = "failed";
  }

  const after = snapshot(operationResult, operationError);
  const failedResult = operationError
    || operationResult === "media-not-found"
    || operationResult === "button-not-found"
    || operationResult === "invalid-position"
    || operationResult === "invalid-volume"
    || operationResult === "invalid-muted"
    || operationResult === "volume-mismatch"
    || operationResult === "muted-mismatch"
    || operationResult === "desired-apply-failed"
    || operationResult === "seek-out-of-range"
    || operationResult === "seek-not-supported"
    || operationResult === "advertisement-active"
    || operationResult === "seek-requested"
    || operationResult === "play-requested"
    || operationResult === "play-rejected"
    || operationResult === "unsupported-operation";
  after.success = !failedResult;
  after.errorCode = failedResult
    ? operationResult === "media-not-found"
      ? "{{IpcConstants.ErrorCodeMediaNotFound}}"
      : operationResult === "invalid-position" || operationResult === "invalid-volume" || operationResult === "invalid-muted"
        ? "{{IpcConstants.ErrorCodeInvalidParameter}}"
        : operationResult === "volume-mismatch"
          ? "{{IpcConstants.ErrorCodeVolumeMismatch}}"
          : operationResult === "muted-mismatch"
            ? "{{IpcConstants.ErrorCodeMutedMismatch}}"
            : operationResult === "seek-out-of-range"
              ? "{{IpcConstants.ErrorCodeSeekOutOfRange}}"
              : operationResult === "seek-not-supported"
                ? "{{IpcConstants.ErrorCodeSeekNotSupported}}"
                : operationResult === "advertisement-active"
                  ? "{{IpcConstants.ErrorCodeAdvertisementActive}}"
                  : operationResult === "play-rejected"
                    ? "{{IpcConstants.ErrorCodePlayRejected}}"
                    : operationResult === "play-requested"
                      ? "{{IpcConstants.ErrorCodePlayVerificationFailed}}"
                      : operationResult === "seek-requested"
                        ? "{{IpcConstants.ErrorCodeTimeout}}"
                        : "{{IpcConstants.ErrorCodeMediaOperationFailed}}"
    : null;
  after.message = failedResult ? (operationError || operationErrorForResult(operationResult) || "Player operation failed.") : "Player operation completed.";
  return JSON.stringify(after);
})()
""";
    }
}
