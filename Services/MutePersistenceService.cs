using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using Microsoft.Web.WebView2.Core;

namespace LiteTubeDock.Services;

public static class MutePersistenceService
{
    private const string ActionInspect = "inspect";
    private const string ActionSet = "set";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<MutePersistenceResult> InspectAsync(
        CoreWebView2 webView,
        bool enabled,
        bool? desiredMutedState,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionInspect, enabled, desiredMutedState, "inspect", cancellationToken);
    }

    public static async Task<MutePersistenceResult> SetAsync(
        CoreWebView2 webView,
        bool enabled,
        bool? desiredMutedState,
        string reason,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionSet, enabled, desiredMutedState, reason, cancellationToken);
    }

    private static async Task<MutePersistenceResult> ExecuteAsync(
        CoreWebView2 webView,
        string action,
        bool enabled,
        bool? desiredMutedState,
        string reason,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var resultJson = await webView.ExecuteScriptAsync(CreateScript(action, enabled, desiredMutedState, reason))
                .WaitAsync(cancellationToken);
            var payload = DecodeScriptResult(resultJson);
            var result = JsonSerializer.Deserialize<MutePersistenceResult>(payload, JsonOptions)
                ?? new MutePersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeMuteStateUnknown,
                    Message = "ミュート継続状態を取得できませんでした。"
                };

            return ApplyDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            return ApplyDuration(
                new MutePersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeTimeout,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    MutePersistenceEnabled = enabled,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ApplyDuration(
                new MutePersistenceResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = ex.Message,
                    OperationError = ex.Message,
                    MutePersistenceEnabled = enabled,
                    DesiredMutedState = desiredMutedState
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static MutePersistenceResult ApplyDuration(MutePersistenceResult result, int durationMs)
    {
        return new MutePersistenceResult
        {
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = result.CurrentUrl,
            MutePersistenceEnabled = result.MutePersistenceEnabled,
            DesiredMutedState = result.DesiredMutedState,
            ActualMutedState = result.ActualMutedState,
            ActualMutedStateBefore = result.ActualMutedStateBefore,
            ActualMutedStateAfter = result.ActualMutedStateAfter,
            MediaElementCount = result.MediaElementCount,
            MediaElementChanged = result.MediaElementChanged,
            LastMuteReapplyReason = result.LastMuteReapplyReason,
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
            Debug.WriteLine($"Mute persistence raw script result was not a JSON string. TargetType={nameof(MutePersistenceResult)}; Exception={ex.Message}; Raw={resultJson}");
            DiagnosticLogService.Write("IPC", $"Event=ScriptResultDecode; Service=MutePersistence; TargetType={nameof(MutePersistenceResult)}; ExceptionType={ex.GetType().Name}; Message={ex.Message}; Raw={resultJson}");
            return resultJson;
        }
    }

    private static string CreateScript(string action, bool enabled, bool? desiredMutedState, string reason)
    {
        var desiredLiteral = desiredMutedState.HasValue
            ? desiredMutedState.Value.ToString().ToLowerInvariant()
            : "null";
        var reasonJson = JsonSerializer.Serialize(reason);

        return $$"""
(() => {
  const action = "{{action}}";
  const requestedEnabled = {{enabled.ToString().ToLowerInvariant()}};
  const requestedDesired = {{desiredLiteral}};
  const reason = {{reasonJson}};
  const url = location.href || "";
  const host = location.hostname || "";
  const youtubeDetected = /(^|\.)youtube\.com$/i.test(host) || /(^|\.)youtu\.be$/i.test(host);
  const state = window.__ltdMutePersistence || {
    enabled: false,
    desiredMutedState: null,
    applying: false,
    mediaKey: "",
    lastReason: "",
    lastResult: "",
    lastErrorCode: null,
    lastMediaCount: 0,
    observer: null,
    observed: new WeakSet(),
    reapplyTimer: 0
  };
  window.__ltdMutePersistence = state;

  const allMedia = () => Array.from(document.querySelectorAll("video,audio"));
  const area = (element) => {
    const rect = element.getBoundingClientRect();
    return Math.max(0, rect.width) * Math.max(0, rect.height);
  };
  const score = (element) => {
    const style = window.getComputedStyle(element);
    const visible = style.display !== "none" && style.visibility !== "hidden" && area(element) > 0;
    let value = 0;
    if (element.tagName.toLowerCase() === "video") value += 1000;
    if (!element.paused) value += 800;
    if (visible) value += 600;
    if (element.readyState >= 2) value += 300;
    if (element.currentSrc || element.src) value += 200;
    value += Math.min(area(element), 100000) / 1000;
    return value;
  };
  const targetMedia = () => allMedia()
    .filter((element) => typeof element.play === "function")
    .sort((a, b) => score(b) - score(a))[0] || null;
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
    return {
      media,
      target,
      count: media.length,
      key: mediaKey(target, media.length),
      actual: target ? Boolean(target.muted) : null
    };
  };
  const result = (success, errorCode, message, before, after, changed, applyReason, applyResult, operationError) => {
    const current = snapshot();
    return JSON.stringify({
      success,
      errorCode: errorCode || null,
      message: message || null,
      currentUrl: url,
      mutePersistenceEnabled: Boolean(state.enabled),
      desiredMutedState: state.desiredMutedState,
      actualMutedState: current.actual,
      actualMutedStateBefore: before,
      actualMutedStateAfter: after,
      mediaElementCount: current.count,
      mediaElementChanged: Boolean(changed),
      lastMuteReapplyReason: state.lastReason || "",
      reapplyReason: applyReason || "",
      reapplyResult: applyResult || "",
      operationError: operationError || null
    });
  };
  const reapply = (applyReason) => {
    const before = snapshot();
    let after = before.actual;
    let applyResult = "skipped";
    let operationError = "";
    let changed = before.key !== state.mediaKey || before.count !== state.lastMediaCount;
    state.mediaKey = before.key;
    state.lastMediaCount = before.count;

    if (!state.enabled) {
      applyResult = "disabled";
    } else if (state.desiredMutedState === null) {
      state.desiredMutedState = before.actual;
      applyResult = before.actual === null ? "desired-unknown" : "desired-captured";
    } else if (!before.target) {
      applyResult = "media-not-found";
      state.lastErrorCode = "{{IpcConstants.ErrorCodeMediaNotFound}}";
    } else if (before.actual === state.desiredMutedState) {
      applyResult = "already-matched";
    } else {
      try {
        state.applying = true;
        before.target.muted = state.desiredMutedState;
        after = Boolean(before.target.muted);
        applyResult = after === state.desiredMutedState ? "reapplied" : "mismatch-after-apply";
        if (applyResult !== "reapplied") {
          state.lastErrorCode = "{{IpcConstants.ErrorCodeMuteReapplyFailed}}";
        }
      } catch (error) {
        operationError = error && (error.name || error.message)
          ? `${error.name || "Error"}: ${error.message || ""}`
          : "Mute reapply failed.";
        applyResult = "failed";
        state.lastErrorCode = "{{IpcConstants.ErrorCodeMuteReapplyFailed}}";
      } finally {
        state.applying = false;
      }
    }

    state.lastReason = applyReason;
    state.lastResult = applyResult;
    return { before: before.actual, after, changed, applyResult, operationError };
  };
  const scheduleReapply = (applyReason) => {
    if (!state.enabled) return;
    window.clearTimeout(state.reapplyTimer);
    state.reapplyTimer = window.setTimeout(() => reapply(applyReason), 120);
  };
  const attachMedia = (element) => {
    if (state.observed.has(element)) return;
    state.observed.add(element);
    ["loadedmetadata", "canplay", "play"].forEach((eventName) => {
      element.addEventListener(eventName, () => scheduleReapply(eventName), true);
    });
    element.addEventListener("volumechange", () => {
      if (state.applying) return;
      state.desiredMutedState = Boolean(element.muted);
      state.lastReason = "user-volumechange";
      state.lastResult = "desired-updated";
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
    state.observer.observe(document.documentElement || document.body, { childList: true, subtree: true });
  };

  if (!youtubeDetected) {
    state.enabled = requestedEnabled;
    if (action === "set" && requestedDesired !== null) {
      state.desiredMutedState = requestedDesired;
    }
    return result(
      true,
      "{{IpcConstants.ErrorCodeMutePersistenceNotSupported}}",
      "YouTube以外のページではミュート継続は何もしません。",
      null,
      null,
      false,
      reason,
      "not-supported",
      null);
  }

  if (action === "inspect") {
    attachAll();
    const current = snapshot();
    return result(true, null, "ミュート継続状態を取得しました。", current.actual, current.actual, false, reason, "inspect", null);
  }

  const before = snapshot();
  state.enabled = requestedEnabled;
  if (requestedEnabled) {
    state.desiredMutedState = requestedDesired === null ? before.actual : requestedDesired;
    ensureObserver();
  } else if (requestedDesired !== null) {
    state.desiredMutedState = requestedDesired;
  }

  const applied = reapply(reason);
  const errorCode = applied.applyResult === "failed" || applied.applyResult === "mismatch-after-apply"
    ? "{{IpcConstants.ErrorCodeMuteReapplyFailed}}"
    : null;
  return result(
    errorCode === null,
    errorCode,
    errorCode === null ? "ミュート継続設定を反映しました。" : "ミュート継続の反映に失敗しました。",
    applied.before,
    applied.after,
    applied.changed,
    reason,
    applied.applyResult,
    applied.operationError);
})()
""";
    }
}
