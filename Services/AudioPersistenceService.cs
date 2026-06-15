using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using Microsoft.Web.WebView2.Core;

namespace LiteTubeDock.Services;

public static class AudioPersistenceService
{
    private const string ActionInspect = "inspect";
    private const string ActionSet = "set";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<AudioPersistenceResult> InspectAsync(
        CoreWebView2 webView,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionInspect, desiredVolumePercent, desiredMutedState, "inspect", cancellationToken);
    }

    public static async Task<AudioPersistenceResult> SetAsync(
        CoreWebView2 webView,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        string reason,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionSet, desiredVolumePercent, desiredMutedState, reason, cancellationToken);
    }

    private static async Task<AudioPersistenceResult> ExecuteAsync(
        CoreWebView2 webView,
        string action,
        int? desiredVolumePercent,
        bool? desiredMutedState,
        string reason,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var resultJson = await webView.ExecuteScriptAsync(CreateScript(action, desiredVolumePercent, desiredMutedState, reason))
                .WaitAsync(cancellationToken);
            var payload = DecodeScriptResult(resultJson);
            var result = JsonSerializer.Deserialize<AudioPersistenceResult>(payload, JsonOptions)
                ?? new AudioPersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeAudioStatusUnavailable,
                    Message = "Audio persistence response was empty."
                };

            return ApplyDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            return ApplyDuration(
                new AudioPersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeTimeout,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    DesiredVolumePercent = desiredVolumePercent,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Audio persistence script execution failed: {ex.Message}");
            return ApplyDuration(
                new AudioPersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    DesiredVolumePercent = desiredVolumePercent,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static AudioPersistenceResult ApplyDuration(AudioPersistenceResult result, int durationMs)
    {
        return new AudioPersistenceResult
        {
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = result.CurrentUrl,
            DesiredVolumePercent = result.DesiredVolumePercent,
            ActualVolumePercent = result.ActualVolumePercent,
            DesiredMutedState = result.DesiredMutedState,
            ActualMutedState = result.ActualMutedState,
            MediaElementCount = result.MediaElementCount,
            MediaElementChanged = result.MediaElementChanged,
            MediaIdentity = result.MediaIdentity,
            MediaRevision = result.MediaRevision,
            TargetElementTag = result.TargetElementTag,
            CurrentSrc = result.CurrentSrc,
            ReadyState = result.ReadyState,
            ReapplyReason = result.ReapplyReason,
            ReapplyResult = result.ReapplyResult,
            OperationError = result.OperationError,
            DurationMs = durationMs
        };
    }

    private static string DecodeScriptResult(string resultJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(resultJson, JsonOptions) ?? resultJson;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Audio persistence raw script result was not a JSON string. TargetType={nameof(AudioPersistenceResult)}; Exception={ex.Message}; Raw={resultJson}");
            DiagnosticLogService.Write("IPC", $"Event=ScriptResultDecode; Service=AudioPersistence; TargetType={nameof(AudioPersistenceResult)}; ExceptionType={ex.GetType().Name}; Message={ex.Message}; Raw={resultJson}");
            return resultJson;
        }
    }

    private static string CreateScript(string action, int? desiredVolumePercent, bool? desiredMutedState, string reason)
    {
        var volumeLiteral = desiredVolumePercent.HasValue
            ? desiredVolumePercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "null";
        var mutedLiteral = desiredMutedState.HasValue
            ? desiredMutedState.Value.ToString().ToLowerInvariant()
            : "null";
        var reasonJson = JsonSerializer.Serialize(reason);

        return $$"""
(() => {
  const action = "{{action}}";
  const requestedDesiredVolumePercent = {{volumeLiteral}};
  const requestedDesiredMuted = {{mutedLiteral}};
  const reason = {{reasonJson}};
  const url = location.href || "";
  const finite = (value) => Number.isFinite(value) ? value : null;
  const percent = (value) => Number.isFinite(value) ? Math.round(value * 100) : null;
  const clampVolumePercent = (value) => {
    if (!Number.isFinite(value)) return null;
    return Math.max(0, Math.min(100, Math.round(value)));
  };
  const hash = (value) => {
    let h = 2166136261;
    for (let i = 0; i < value.length; i++) {
      h ^= value.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return (h >>> 0).toString(16);
  };
  const state = window.__ltdAudioPersistence || {
    desiredVolumePercent: null,
    desiredMutedState: null,
    applying: false,
    mediaKey: "",
    identity: "",
    revision: 0,
    lastMediaCount: 0,
    observer: null,
    observed: new WeakSet(),
    reapplyTimer: 0
  };
  window.__ltdAudioPersistence = state;

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
  const mediaIdentity = (media) => {
    if (!media) return "";
    const duration = Number.isFinite(media.duration) ? media.duration.toFixed(3) : "live";
    return hash([
      media.currentSrc || media.src || "",
      location.href || "",
      document.title || "",
      duration,
      media.tagName.toLowerCase()
    ].join("|"));
  };
  const mediaKey = (media, count) => {
    if (!media) return "";
    return [
      media.tagName.toLowerCase(),
      media.currentSrc || media.src || "",
      media.readyState,
      Math.round(Number.isFinite(media.currentTime) ? media.currentTime : 0),
      count
    ].join("|");
  };
  const snapshot = () => {
    const media = allMedia();
    const target = targetMedia();
    const identity = mediaIdentity(target);
    if (identity && state.identity !== identity) {
      state.identity = identity;
      state.revision += 1;
    }
    return {
      media,
      target,
      count: media.length,
      key: mediaKey(target, media.length),
      identity,
      volumePercent: target ? percent(target.volume) : null,
      muted: target ? Boolean(target.muted) : null
    };
  };
  const result = (success, errorCode, message, snap, changed, applyReason, applyResult, operationError) => JSON.stringify({
    success,
    errorCode: errorCode || null,
    message: message || null,
    currentUrl: url,
    desiredVolumePercent: state.desiredVolumePercent,
    actualVolumePercent: snap ? snap.volumePercent : null,
    desiredMutedState: state.desiredMutedState,
    actualMutedState: snap ? snap.muted : null,
    mediaElementCount: snap ? snap.count : 0,
    mediaElementChanged: Boolean(changed),
    mediaIdentity: snap ? snap.identity : "",
    mediaRevision: state.revision,
    targetElementTag: snap && snap.target ? snap.target.tagName.toLowerCase() : "",
    currentSrc: snap && snap.target ? (snap.target.currentSrc || snap.target.src || "") : "",
    readyState: snap && snap.target ? snap.target.readyState : null,
    reapplyReason: applyReason || "",
    reapplyResult: applyResult || "",
    operationError: operationError || null
  });
  const reapply = (applyReason) => {
    const before = snapshot();
    let changed = before.key !== state.mediaKey || before.count !== state.lastMediaCount;
    state.mediaKey = before.key;
    state.lastMediaCount = before.count;
    if (state.desiredVolumePercent === null && state.desiredMutedState === null) {
      return { snap: before, changed, applyResult: "no-desired-state", operationError: "" };
    }
    if (!before.target) {
      return { snap: before, changed, applyResult: "media-not-found", operationError: "" };
    }

    let operationError = "";
    try {
      state.applying = true;
      if (state.desiredVolumePercent !== null) {
        before.target.volume = state.desiredVolumePercent / 100;
      }
      if (state.desiredMutedState !== null) {
        before.target.muted = state.desiredMutedState;
      }
    } catch (error) {
      operationError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : "Audio persistence apply failed.";
    } finally {
      state.applying = false;
    }

    const after = snapshot();
    const volumeMatched = state.desiredVolumePercent === null || after.volumePercent === state.desiredVolumePercent;
    const mutedMatched = state.desiredMutedState === null || after.muted === state.desiredMutedState;
    const applyResult = operationError
      ? "failed"
      : volumeMatched && mutedMatched
        ? "reapplied"
        : "mismatch-after-apply";
    return { snap: after, changed, applyResult, operationError };
  };
  const scheduleReapply = (applyReason) => {
    if (state.desiredVolumePercent === null && state.desiredMutedState === null) return;
    window.clearTimeout(state.reapplyTimer);
    state.reapplyTimer = window.setTimeout(() => reapply(applyReason), 80);
  };
  const attachMedia = (element) => {
    if (state.observed.has(element)) return;
    state.observed.add(element);
    ["loadedmetadata", "canplay", "play", "playing", "emptied", "durationchange", "loadeddata"].forEach((eventName) => {
      element.addEventListener(eventName, () => {
        if (eventName === "play" || eventName === "playing") {
          element.__ltdLastPlayAt = Date.now();
        }
        scheduleReapply(eventName);
      }, true);
    });
    element.addEventListener("volumechange", () => {
      if (state.applying) return;
      state.desiredVolumePercent = percent(element.volume);
      state.desiredMutedState = Boolean(element.muted);
    }, true);
  };
  const attachAll = () => {
    allMedia().forEach(attachMedia);
  };
  const ensureObserver = () => {
    attachAll();
    if (state.observer) return;
    state.observer = new MutationObserver(() => {
      attachAll();
      scheduleReapply("mutation");
    });
    state.observer.observe(document.documentElement || document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["src"]
    });
  };

  ensureObserver();
  if (action === "set") {
    if (requestedDesiredVolumePercent !== null) {
      state.desiredVolumePercent = clampVolumePercent(requestedDesiredVolumePercent);
    }
    if (requestedDesiredMuted !== null) {
      state.desiredMutedState = Boolean(requestedDesiredMuted);
    }
  }

  const applied = action === "set" ? reapply(reason) : { snap: snapshot(), changed: false, applyResult: "inspect", operationError: "" };
  const ok = applied.applyResult !== "failed" && applied.applyResult !== "mismatch-after-apply" && applied.applyResult !== "media-not-found";
  const errorCode = ok ? null : "{{IpcConstants.ErrorCodeMediaOperationFailed}}";
  return result(
    ok,
    errorCode,
    ok ? "Audio persistence state was applied." : "Audio persistence state could not be applied.",
    applied.snap,
    applied.changed,
    reason,
    applied.applyResult,
    applied.operationError);
})()
""";
    }
}
