using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using Microsoft.Web.WebView2.Core;

namespace LiteTubeDock.Services;

public static class MediaControlService
{
    private const string InspectAction = "inspect";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<MediaControlResult> ExecuteAsync(
        CoreWebView2 webView,
        string command,
        CancellationToken cancellationToken,
        Action<MediaControlResult>? attemptLogger = null)
    {
        return await ExecuteInternalAsync(webView, command, retryWhenNotFound: true, cancellationToken, attemptLogger);
    }

    public static async Task<MediaControlResult> InspectAsync(
        CoreWebView2 webView,
        CancellationToken cancellationToken)
    {
        return await ExecuteInternalAsync(webView, InspectAction, retryWhenNotFound: false, cancellationToken, attemptLogger: null);
    }

    public static async Task<AudioControlResult> GetAudioStatusAsync(
        CoreWebView2 webView,
        int? desiredVolumePercent,
        bool mutePersistenceEnabled,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        return await ExecuteAudioAsync(
            webView,
            IpcConstants.CommandGetAudioStatus,
            requestedVolumePercent: null,
            requestedMuted: null,
            desiredVolumePercent,
            mutePersistenceEnabled,
            desiredMutedState,
            cancellationToken);
    }

    public static async Task<AudioControlResult> SetVolumeAsync(
        CoreWebView2 webView,
        int volumePercent,
        int? desiredVolumePercent,
        bool mutePersistenceEnabled,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        return await ExecuteAudioAsync(
            webView,
            IpcConstants.CommandSetVolume,
            volumePercent,
            requestedMuted: null,
            desiredVolumePercent,
            mutePersistenceEnabled,
            desiredMutedState,
            cancellationToken);
    }

    public static async Task<AudioControlResult> SetMutedAsync(
        CoreWebView2 webView,
        bool muted,
        int? desiredVolumePercent,
        bool mutePersistenceEnabled,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        return await ExecuteAudioAsync(
            webView,
            IpcConstants.CommandSetMuted,
            requestedVolumePercent: null,
            muted,
            desiredVolumePercent,
            mutePersistenceEnabled,
            desiredMutedState,
            cancellationToken);
    }

    public static async Task<SeekControlResult> SeekToAsync(
        CoreWebView2 webView,
        double positionSeconds,
        CancellationToken cancellationToken)
    {
        return await ExecuteSeekAsync(webView, positionSeconds, cancellationToken);
    }

    private static async Task<MediaControlResult> ExecuteInternalAsync(
        CoreWebView2 webView,
        string command,
        bool retryWhenNotFound,
        CancellationToken cancellationToken,
        Action<MediaControlResult>? attemptLogger)
    {
        var stopwatch = Stopwatch.StartNew();
        MediaControlResult? lastResult = null;
        var maxAttempts = retryWhenNotFound ? 3 : 1;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await ExecuteAttemptAsync(webView, command, attempt, stopwatch, cancellationToken);
            var timedResult = ApplyDuration(result, (int)stopwatch.ElapsedMilliseconds);
            attemptLogger?.Invoke(timedResult);
            lastResult = timedResult;
            if (timedResult.MediaFound || attempt == maxAttempts)
            {
                return timedResult;
            }

            await Task.Delay(250, cancellationToken);
        }

        return lastResult
            ?? ApplyDuration(
                new MediaControlResult
            {
                Success = false,
                MediaFound = false,
                ErrorCode = IpcConstants.ErrorCodeMediaNotFound
            },
            (int)stopwatch.ElapsedMilliseconds);
    }

    private static async Task<MediaControlResult> ExecuteAttemptAsync(
        CoreWebView2 webView,
        string command,
        int attempt,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var script = CreateScript(command, attempt);
        string resultJson;
        try
        {
            resultJson = await webView.ExecuteScriptAsync(script).WaitAsync(cancellationToken);
        }
        catch (TimeoutException ex)
        {
            Debug.WriteLine($"Media control script timed out: {ex.Message}");
            return new MediaControlResult
            {
                Success = false,
                MediaFound = false,
                ErrorCode = IpcConstants.ErrorCodeTimeout,
                Message = ex.Message,
                AttemptCount = attempt,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media control script execution failed: {ex.Message}");
            return new MediaControlResult
            {
                Success = false,
                MediaFound = false,
                ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                Message = ex.Message,
                AttemptCount = attempt,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        try
        {
            var payload = DecodeScriptResult(resultJson, nameof(MediaControlResult));
            return JsonSerializer.Deserialize<MediaControlResult>(payload, JsonOptions)
                ?? new MediaControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    AttemptCount = attempt
                };
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Media control response parse failed: {ex.Message}");
            return new MediaControlResult
            {
                Success = false,
                MediaFound = false,
                ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                Message = ex.Message,
                AttemptCount = attempt,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static async Task<AudioControlResult> ExecuteAudioAsync(
        CoreWebView2 webView,
        string command,
        int? requestedVolumePercent,
        bool? requestedMuted,
        int? desiredVolumePercent,
        bool mutePersistenceEnabled,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var script = CreateScript(
            command,
            attempt: 1,
            requestedVolumePercent,
            requestedMuted,
            desiredVolumePercent,
            mutePersistenceEnabled,
            desiredMutedState);
        try
        {
            var resultJson = await webView.ExecuteScriptAsync(script).WaitAsync(cancellationToken);
            var payload = DecodeScriptResult(resultJson, nameof(AudioControlResult));
            var result = JsonSerializer.Deserialize<AudioControlResult>(payload, JsonOptions)
                ?? new AudioControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeAudioStatusUnavailable,
                    Message = "Audio status response was empty."
                };

            return ApplyAudioDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            return ApplyAudioDuration(
                new AudioControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeTimeout,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    RequestedVolumePercent = requestedVolumePercent,
                    RequestedMuted = requestedMuted,
                    DesiredVolumePercent = desiredVolumePercent,
                    MutePersistenceEnabled = mutePersistenceEnabled,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ApplyAudioDuration(
                new AudioControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    RequestedVolumePercent = requestedVolumePercent,
                    RequestedMuted = requestedMuted,
                    DesiredVolumePercent = desiredVolumePercent,
                    MutePersistenceEnabled = mutePersistenceEnabled,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<SeekControlResult> ExecuteSeekAsync(
        CoreWebView2 webView,
        double positionSeconds,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var script = CreateScript(
            IpcConstants.CommandSeekTo,
            attempt: 1,
            requestedVolumePercent: null,
            requestedMuted: null,
            desiredVolumePercent: null,
            mutePersistenceEnabled: false,
            desiredMutedState: null,
            requestedPositionSeconds: positionSeconds);
        try
        {
            var resultJson = await webView.ExecuteScriptAsync(script).WaitAsync(cancellationToken);
            var payload = DecodeScriptResult(resultJson, nameof(SeekControlResult));
            var result = JsonSerializer.Deserialize<SeekControlResult>(payload, JsonOptions)
                ?? new SeekControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeUnknownError,
                    Message = "Seek response was empty.",
                    RequestedPositionSeconds = positionSeconds
                };

            return ApplySeekDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            return ApplySeekDuration(
                new SeekControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeTimeout,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    RequestedPositionSeconds = positionSeconds
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ApplySeekDuration(
                new SeekControlResult
                {
                    Success = false,
                    MediaFound = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    RequestedPositionSeconds = positionSeconds
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static MediaControlResult ApplyDuration(MediaControlResult result, int durationMs)
    {
        return new MediaControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            DocumentReadyState = result.DocumentReadyState,
            VideoElementCount = result.VideoElementCount,
            AudioElementCount = result.AudioElementCount,
            IframeElementCount = result.IframeElementCount,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            NetworkState = result.NetworkState,
            VideoWidth = result.VideoWidth,
            VideoHeight = result.VideoHeight,
            DisplayWidth = result.DisplayWidth,
            DisplayHeight = result.DisplayHeight,
            Display = result.Display,
            Visibility = result.Visibility,
            Opacity = result.Opacity,
            AttemptCount = result.AttemptCount,
            OperationError = result.OperationError,
            IsPaused = result.IsPaused,
            BeforeMuted = result.BeforeMuted,
            AfterMuted = result.AfterMuted,
            IsMuted = result.IsMuted,
            BeforeCurrentTime = result.BeforeCurrentTime,
            AfterCurrentTime = result.AfterCurrentTime,
            CurrentTime = result.CurrentTime,
            Duration = result.Duration,
            DurationMs = durationMs
        };
    }

    private static string DecodeScriptResult(string resultJson, string targetType)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(resultJson, JsonOptions) ?? resultJson;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Media control raw script result was not a JSON string. TargetType={targetType}; Exception={ex.Message}; Raw={resultJson}");
            DiagnosticLogService.Write("IPC", $"Event=ScriptResultDecode; Service=MediaControl; TargetType={targetType}; ExceptionType={ex.GetType().Name}; Message={ex.Message}; Raw={resultJson}");
            return resultJson;
        }
    }

    private static AudioControlResult ApplyAudioDuration(AudioControlResult result, int durationMs)
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
            DesiredVolumePercent = result.DesiredVolumePercent,
            IsMuted = result.IsMuted,
            RequestedMuted = result.RequestedMuted,
            ActualMuted = result.ActualMuted,
            MutePersistenceEnabled = result.MutePersistenceEnabled,
            DesiredMutedState = result.DesiredMutedState,
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
            DurationMs = durationMs
        };
    }

    private static SeekControlResult ApplySeekDuration(SeekControlResult result, int durationMs)
    {
        return new SeekControlResult
        {
            Success = result.Success,
            MediaFound = result.MediaFound,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            RequestedPositionSeconds = result.RequestedPositionSeconds,
            ActualPositionSeconds = result.ActualPositionSeconds,
            Duration = result.Duration,
            DurationSeconds = result.DurationSeconds,
            IsSeekable = result.IsSeekable,
            IsLive = result.IsLive,
            CurrentUrl = result.CurrentUrl,
            MediaTitle = result.MediaTitle,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            MediaElementCount = result.MediaElementCount,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            OperationError = result.OperationError,
            DurationMs = durationMs
        };
    }

    private static string CreateScript(
        string command,
        int attempt,
        int? requestedVolumePercent = null,
        bool? requestedMuted = null,
        int? desiredVolumePercent = null,
        bool mutePersistenceEnabled = false,
        bool? desiredMutedState = null,
        double? requestedPositionSeconds = null)
    {
        var action = command switch
        {
            InspectAction => InspectAction,
            IpcConstants.CommandPlay => "play",
            IpcConstants.CommandPause => "pause",
            IpcConstants.CommandToggleMute => "toggle-mute",
            IpcConstants.CommandSeekToStart => "seek-to-start",
            IpcConstants.CommandSeekTo => "seek-to",
            IpcConstants.CommandGetAudioStatus => "get-audio-status",
            IpcConstants.CommandSetVolume => "set-volume",
            IpcConstants.CommandSetMuted => "set-muted",
            _ => string.Empty
        };
        var volumeLiteral = requestedVolumePercent.HasValue
            ? requestedVolumePercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "null";
        var mutedLiteral = requestedMuted.HasValue
            ? requestedMuted.Value.ToString().ToLowerInvariant()
            : "null";
        var desiredVolumeLiteral = desiredVolumePercent.HasValue
            ? desiredVolumePercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "null";
        var desiredMutedLiteral = desiredMutedState.HasValue
            ? desiredMutedState.Value.ToString().ToLowerInvariant()
            : "null";
        var positionLiteral = requestedPositionSeconds.HasValue
            ? requestedPositionSeconds.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            : "null";

        return $$"""
(() => {
  const action = "{{action}}";
  const requestedVolumePercent = {{volumeLiteral}};
  const requestedMuted = {{mutedLiteral}};
  const desiredVolumePercent = {{desiredVolumeLiteral}};
  const mutePersistenceEnabled = {{mutePersistenceEnabled.ToString().ToLowerInvariant()}};
  const desiredMutedState = {{desiredMutedLiteral}};
  const requestedPositionSeconds = {{positionLiteral}};
  const finite = (value) => Number.isFinite(value) ? value : null;
  const percent = (value) => Number.isFinite(value) ? Math.round(value * 100) : null;
  const mediaTitle = () => {
    const title = document.title || "";
    return title.replace(/\s*-\s*YouTube\s*$/i, "");
  };
  const hash = (value) => {
    let h = 2166136261;
    for (let i = 0; i < value.length; i++) {
      h ^= value.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return (h >>> 0).toString(16);
  };
  const mediaTracking = () => {
    const state = window.__ltdMediaTracking || { identity: "", revision: 0 };
    window.__ltdMediaTracking = state;
    return state;
  };

  const snapshot = () => {
    const videos = Array.from(document.querySelectorAll("video"));
    const audios = Array.from(document.querySelectorAll("audio"));
    const iframes = Array.from(document.querySelectorAll("iframe"));
    const all = videos.concat(audios);
    return {
      readyState: document.readyState || "",
      videos,
      audios,
      iframes,
      all
    };
  };

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
    if (!visible) value -= 500;
    if (element.readyState === 0 && !hasSource) value -= 400;
    return value;
  };

  const state = snapshot();
  const target = state.all
    .filter((element) => typeof element.play === "function")
    .sort((a, b) => score(b) - score(a))[0] || null;

  const base = {
    documentReadyState: state.readyState,
    videoElementCount: state.videos.length,
    audioElementCount: state.audios.length,
    iframeElementCount: state.iframes.length,
    mediaElementCount: state.all.length,
    currentUrl: location.href || "",
    mediaTitle: mediaTitle(),
    desiredVolumePercent,
    mutePersistenceEnabled,
    desiredMutedState,
    requestedPositionSeconds,
    attemptCount: {{attempt}}
  };

  if (!target) {
    return JSON.stringify({
      ...base,
      success: false,
      mediaFound: false,
      errorCode: "{{IpcConstants.ErrorCodeMediaNotFound}}",
      message: "Media element was not found."
    });
  }

  const beforeMuted = target.muted;
  const beforeCurrentTime = finite(target.currentTime);
  const style = window.getComputedStyle(target);
  const rect = target.getBoundingClientRect();
  const targetInfo = {
    targetElementTag: target.tagName.toLowerCase(),
    currentSrc: target.currentSrc || target.src || "",
    readyState: target.readyState,
    networkState: target.networkState,
    videoWidth: Number.isFinite(target.videoWidth) ? target.videoWidth : null,
    videoHeight: Number.isFinite(target.videoHeight) ? target.videoHeight : null,
    displayWidth: finite(rect.width),
    displayHeight: finite(rect.height),
    display: style.display || "",
    visibility: style.visibility || "",
    opacity: style.opacity || ""
  };
  const seekableRangeCount = target.seekable ? target.seekable.length : 0;
  const isLive = !Number.isFinite(target.duration) || target.duration === Infinity;
  const hasFiniteDuration = Number.isFinite(target.duration) && target.duration >= 0;
  const isSeekable = !isLive && (seekableRangeCount > 0 || hasFiniteDuration);
  const identitySeed = [
    target.currentSrc || target.src || "",
    location.href || "",
    mediaTitle(),
    hasFiniteDuration ? target.duration.toFixed(3) : "live",
    target.tagName.toLowerCase()
  ].join("|");
  const identity = hash(identitySeed);
  const tracking = mediaTracking();
  if (tracking.identity !== identity) {
    tracking.identity = identity;
    tracking.revision += 1;
  }
  const mediaState = () => ({
    currentTime: finite(target.currentTime),
    currentTimeSeconds: finite(target.currentTime),
    duration: finite(target.duration),
    durationSeconds: finite(target.duration),
    isSeekable,
    isLive,
    playbackRate: finite(target.playbackRate),
    isPlaying: !target.paused,
    isPaused: target.paused,
    mediaIdentity: identity,
    mediaRevision: tracking.revision
  });

  const audioInfo = () => ({
    volume: finite(target.volume),
    volumePercent: percent(target.volume),
    appliedVolume: finite(target.volume),
    appliedVolumePercent: percent(target.volume),
    isMuted: target.muted,
    actualMuted: target.muted,
    ...mediaState()
  });

  if (action === "inspect") {
    return JSON.stringify({
      ...base,
      ...targetInfo,
      success: true,
      mediaFound: true,
      isPaused: target.paused,
      isMuted: target.muted,
      currentTime: finite(target.currentTime),
      duration: finite(target.duration)
    });
  }

  if (action === "get-audio-status") {
    return JSON.stringify({
      ...base,
      ...targetInfo,
      ...audioInfo(),
      success: true,
      mediaFound: true,
      message: "Audio status was retrieved."
    });
  }

  if (action === "seek-to") {
    const seekBase = {
      ...base,
      ...targetInfo,
      ...mediaState(),
      requestedPositionSeconds,
      actualPositionSeconds: finite(target.currentTime)
    };

    if (target.readyState === 0 && !target.currentSrc && !target.src) {
      return JSON.stringify({
        ...seekBase,
        success: false,
        mediaFound: true,
        errorCode: "{{IpcConstants.ErrorCodeMediaNotReady}}",
        message: "Media is not ready."
      });
    }

    if (!isSeekable) {
      return JSON.stringify({
        ...seekBase,
        success: false,
        mediaFound: true,
        errorCode: "{{IpcConstants.ErrorCodeSeekNotSupported}}",
        message: isLive ? "Live media is not seekable." : "Media is not seekable."
      });
    }

    if (hasFiniteDuration && requestedPositionSeconds > target.duration + 0.001) {
      return JSON.stringify({
        ...seekBase,
        success: false,
        mediaFound: true,
        errorCode: "{{IpcConstants.ErrorCodePositionOutOfRange}}",
        message: "positionSeconds exceeds media duration."
      });
    }

    try {
      target.currentTime = requestedPositionSeconds;
      const actual = finite(target.currentTime);
      const matched = actual !== null && Math.abs(actual - requestedPositionSeconds) <= 1.0;
      return JSON.stringify({
        ...seekBase,
        ...mediaState(),
        success: matched || target.seeking,
        mediaFound: true,
        errorCode: matched || target.seeking ? null : "{{IpcConstants.ErrorCodeTimeout}}",
        message: matched || target.seeking ? "Seek accepted." : "Seek did not complete in time.",
        actualPositionSeconds: actual
      });
    } catch (error) {
      return JSON.stringify({
        ...seekBase,
        success: false,
        mediaFound: true,
        errorCode: "{{IpcConstants.ErrorCodeSeekNotSupported}}",
        message: "Seek failed.",
        operationError: error && (error.name || error.message) ? `${error.name || "Error"}: ${error.message || ""}` : "Seek failed."
      });
    }
  }

  try {
    if (action === "play") {
      const playResult = target.play();
      if (playResult && typeof playResult.catch === "function") {
        playResult.catch(() => {});
      }
    } else if (action === "pause") {
      target.pause();
    } else if (action === "toggle-mute") {
      target.muted = !target.muted;
    } else if (action === "seek-to-start") {
      target.currentTime = 0;
    } else if (action === "set-volume") {
      target.volume = requestedVolumePercent / 100;
    } else if (action === "set-muted") {
      target.muted = requestedMuted;
    } else {
      return JSON.stringify({
        ...base,
        ...targetInfo,
        success: false,
        mediaFound: true,
        errorCode: "{{IpcConstants.ErrorCodeMediaOperationFailed}}",
        message: "Unsupported media action."
      });
    }
  } catch (error) {
    return JSON.stringify({
      ...base,
      ...targetInfo,
      success: false,
      mediaFound: true,
      errorCode: action === "play"
        ? "{{IpcConstants.ErrorCodePlayRejected}}"
        : action === "set-volume"
          ? "{{IpcConstants.ErrorCodeVolumeSetFailed}}"
          : action === "set-muted"
            ? "{{IpcConstants.ErrorCodeMuteSetFailed}}"
            : "{{IpcConstants.ErrorCodeMediaOperationFailed}}",
      operationError: error && (error.name || error.message) ? `${error.name || "Error"}: ${error.message || ""}` : "Media operation failed."
    });
  }

  return JSON.stringify({
    ...base,
    ...targetInfo,
    ...(action === "set-volume" || action === "set-muted" ? audioInfo() : {}),
    success: true,
    mediaFound: true,
    requestedVolumePercent,
    requestedMuted,
    isPaused: target.paused,
    beforeMuted,
    afterMuted: target.muted,
    isMuted: target.muted,
    beforeCurrentTime,
    afterCurrentTime: finite(target.currentTime),
    currentTime: finite(target.currentTime),
    duration: finite(target.duration)
  });
})()
""";
    }
}
