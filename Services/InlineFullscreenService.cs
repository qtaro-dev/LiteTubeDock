using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;
using Microsoft.Web.WebView2.Core;

namespace LiteTubeDock.Services;

public static class InlineFullscreenService
{
    private const string ActionInspect = "inspect";
    private const string ActionEnter = "enter";
    private const string ActionExit = "exit";
    private const string ActionToggle = "toggle";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<InlineFullscreenResult> InspectAsync(
        CoreWebView2 webView,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionInspect, cancellationToken);
    }

    public static async Task<InlineFullscreenResult> EnterAsync(
        CoreWebView2 webView,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionEnter, cancellationToken);
    }

    public static async Task<InlineFullscreenResult> ExitAsync(
        CoreWebView2 webView,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionExit, cancellationToken);
    }

    public static async Task<InlineFullscreenResult> ToggleAsync(
        CoreWebView2 webView,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(webView, ActionToggle, cancellationToken);
    }

    private static async Task<InlineFullscreenResult> ExecuteAsync(
        CoreWebView2 webView,
        string action,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var resultJson = await webView.ExecuteScriptAsync(CreateScript(action)).WaitAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<string>(resultJson, JsonOptions) ?? resultJson;
            var result = JsonSerializer.Deserialize<InlineFullscreenResult>(payload, JsonOptions)
                ?? new InlineFullscreenResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeInlineFullscreenStateUnknown,
                    Message = "枠内全画面の状態を取得できませんでした。"
                };

            return ApplyDuration(result, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            return ApplyDuration(
                new InlineFullscreenResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeTimeout,
                    Message = ex.Message,
                    OperationError = ex.Message
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ApplyDuration(
                new InlineFullscreenResult
                {
                    Success = false,
                    ErrorCode = IpcConstants.ErrorCodeScriptExecutionFailed,
                    Message = ex.Message,
                    OperationError = ex.Message
                },
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private static InlineFullscreenResult ApplyDuration(InlineFullscreenResult result, int durationMs)
    {
        return new InlineFullscreenResult
        {
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
            CurrentUrl = result.CurrentUrl,
            YoutubeDetected = result.YoutubeDetected,
            IsShorts = result.IsShorts,
            InlineFullscreenBefore = result.InlineFullscreenBefore,
            InlineFullscreenAfter = result.InlineFullscreenAfter,
            IsInlineFullscreen = result.IsInlineFullscreen,
            DomOperationResult = result.DomOperationResult,
            FullscreenApiResult = result.FullscreenApiResult,
            OperationError = result.OperationError,
            DurationMs = durationMs
        };
    }

    private static string CreateScript(string action)
    {
        return $$"""
(() => {
  const action = "{{action}}";
  const url = location.href || "";
  const host = location.hostname || "";
  const path = location.pathname || "";
  const youtubeDetected = /(^|\.)youtube\.com$/i.test(host) || /(^|\.)youtu\.be$/i.test(host);
  const isShorts = /\/shorts\//i.test(path);

  const result = {
    success: false,
    currentUrl: url,
    youtubeDetected,
    isShorts,
    inlineFullscreenBefore: null,
    inlineFullscreenAfter: null,
    isInlineFullscreen: null,
    domOperationResult: "",
    fullscreenApiResult: "",
    errorCode: null,
    message: null,
    operationError: null
  };

  const player = document.querySelector("#movie_player")
    || document.querySelector(".html5-video-player")
    || document.querySelector("ytd-player")
    || document.querySelector(".html5-video-container");

  const isInlineFullscreen = () => {
    const p = document.querySelector("#movie_player") || document.querySelector(".html5-video-player");
    const playerApiFullscreen = Boolean(p && typeof p.isFullscreen === "function" && p.isFullscreen());
    const hasFullscreenElement = Boolean(document.fullscreenElement);
    const playerFullscreen = Boolean(p && (
      p.classList.contains("ytp-fullscreen")
      || p.classList.contains("ytp-big-mode")
      || p.classList.contains("ytp-fullscreen-player")
    ));
    return playerApiFullscreen || hasFullscreenElement || playerFullscreen;
  };

  const findButton = () => {
    const selectors = [
      ".ytp-fullscreen-button",
      "button.ytp-fullscreen-button",
      "button[title*='Full screen']",
      "button[aria-label*='Full screen']",
      "button[title*='全画面']",
      "button[aria-label*='全画面']",
      "button[title*='Fullscreen']",
      "button[aria-label*='Fullscreen']"
    ];
    for (const selector of selectors) {
      const button = document.querySelector(selector);
      if (button) return button;
    }
    return null;
  };

  const finalize = (success, errorCode, message) => {
    result.success = success;
    result.errorCode = errorCode || null;
    result.message = message || null;
    result.inlineFullscreenAfter = isInlineFullscreen();
    result.isInlineFullscreen = result.inlineFullscreenAfter;
    return JSON.stringify(result);
  };

  if (!youtubeDetected) {
    result.errorCode = "{{IpcConstants.ErrorCodeYoutubePageNotDetected}}";
    result.message = "YouTubeページではないため、枠内全画面を操作できません。";
    return finalize(action === "inspect", result.errorCode, result.message);
  }

  result.inlineFullscreenBefore = isInlineFullscreen();

  if (action === "inspect") {
    result.domOperationResult = "inspect";
    return finalize(true, null, "枠内全画面状態を取得しました。");
  }

  if (isShorts) {
    result.errorCode = "{{IpcConstants.ErrorCodeInlineFullscreenNotSupported}}";
    result.message = "Shortsページの枠内全画面操作は未対応です。";
    return finalize(false, result.errorCode, result.message);
  }

  if (!player) {
    result.errorCode = "{{IpcConstants.ErrorCodeInlineFullscreenButtonNotFound}}";
    result.message = "YouTubeプレイヤーが見つかりません。";
    return finalize(false, result.errorCode, result.message);
  }

  const desired = action === "toggle" ? !result.inlineFullscreenBefore : action === "enter";
  if (action === "enter" && result.inlineFullscreenBefore) {
    result.domOperationResult = "already-entered";
    return finalize(true, null, "既に枠内全画面です。");
  }

  if (action === "exit" && !result.inlineFullscreenBefore) {
    result.domOperationResult = "already-exited";
    return finalize(true, null, "既に枠内全画面は解除されています。");
  }

  const moviePlayer = document.querySelector("#movie_player");
  if (moviePlayer && typeof moviePlayer.toggleFullscreen === "function") {
    try {
      moviePlayer.toggleFullscreen();
      result.domOperationResult = "movie-player-toggleFullscreen";
      const after = isInlineFullscreen();
      result.inlineFullscreenAfter = after;
      result.isInlineFullscreen = after;
      result.success = true;
      result.errorCode = null;
      result.message = "YouTubeプレイヤーAPIで枠内全画面操作を受け付けました。";
      return JSON.stringify(result);
    } catch (error) {
      result.operationError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : "toggleFullscreen failed.";
    }
  }

  const button = findButton();
  if (button) {
    try {
      button.click();
      result.domOperationResult = "fullscreen-button-clicked";
      const after = isInlineFullscreen();
      result.inlineFullscreenAfter = after;
      result.isInlineFullscreen = after;
      result.success = true;
      result.errorCode = null;
      result.message = "枠内全画面ボタン操作を受け付けました。";
      return JSON.stringify(result);
    } catch (error) {
      result.operationError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : "Button click failed.";
    }
  } else {
    result.domOperationResult = "fullscreen-button-not-found";
  }

  if (desired && player.requestFullscreen) {
    try {
      player.requestFullscreen();
      result.fullscreenApiResult = "requestFullscreen-called";
      result.inlineFullscreenAfter = true;
      result.isInlineFullscreen = true;
      return finalize(true, null, "Fullscreen APIで枠内全画面の開始を要求しました。");
    } catch (error) {
      result.operationError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : "requestFullscreen failed.";
      return finalize(false, "{{IpcConstants.ErrorCodeInlineFullscreenRequestFailed}}", "枠内全画面の開始に失敗しました。");
    }
  }

  if (!desired && document.exitFullscreen) {
    try {
      document.exitFullscreen();
      result.fullscreenApiResult = "exitFullscreen-called";
      result.inlineFullscreenAfter = false;
      result.isInlineFullscreen = false;
      return finalize(true, null, "Fullscreen APIで枠内全画面の解除を要求しました。");
    } catch (error) {
      result.operationError = error && (error.name || error.message)
        ? `${error.name || "Error"}: ${error.message || ""}`
        : "exitFullscreen failed.";
      return finalize(false, "{{IpcConstants.ErrorCodeInlineFullscreenExitFailed}}", "枠内全画面の解除に失敗しました。");
    }
  }

  return finalize(
    false,
    "{{IpcConstants.ErrorCodeInlineFullscreenButtonNotFound}}",
    "枠内全画面ボタンまたはFullscreen APIが見つかりません。");
})()
""";
    }
}
