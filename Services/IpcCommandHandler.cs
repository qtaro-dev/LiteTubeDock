using System.Diagnostics;
using System.Text.Json;
using LiteTubeDock.Constants;
using LiteTubeDock.Models;

namespace LiteTubeDock.Services;

public sealed class IpcCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly int _processId = Environment.ProcessId;
    private readonly Func<string, CancellationToken, Task<bool>> _navigateAsync;
    private readonly Func<string, CancellationToken, Task<MediaControlResult>> _mediaControlAsync;
    private readonly Func<CancellationToken, Task<AudioControlResult>> _getAudioStatusAsync;
    private readonly Func<int, CancellationToken, Task<AudioControlResult>> _setVolumeAsync;
    private readonly Func<bool, CancellationToken, Task<AudioControlResult>> _setMutedAsync;
    private readonly Func<double, CancellationToken, Task<SeekControlResult>> _seekToAsync;
    private readonly Func<string, CancellationToken, Task<InlineFullscreenResult>> _inlineFullscreenAsync;
    private readonly Func<bool?, CancellationToken, Task<MutePersistenceResult>> _setMutePersistenceAsync;
    private readonly Func<CancellationToken, Task<MutePersistenceResult>> _getMutePersistenceAsync;
    private readonly Func<CancellationToken, Task<IpcStatusData>> _getStatusAsync;
    private readonly Func<string, IpcCommand?, CancellationToken, Task<UnifiedPlayerStateResult>> _playerControlAsync;
    private readonly Action<string> _log;
    private readonly Action<string?> _recordError;

    public IpcCommandHandler(
        Func<string, CancellationToken, Task<bool>> navigateAsync,
        Func<string, CancellationToken, Task<MediaControlResult>> mediaControlAsync,
        Func<CancellationToken, Task<AudioControlResult>> getAudioStatusAsync,
        Func<int, CancellationToken, Task<AudioControlResult>> setVolumeAsync,
        Func<bool, CancellationToken, Task<AudioControlResult>> setMutedAsync,
        Func<double, CancellationToken, Task<SeekControlResult>> seekToAsync,
        Func<string, CancellationToken, Task<InlineFullscreenResult>> inlineFullscreenAsync,
        Func<bool?, CancellationToken, Task<MutePersistenceResult>> setMutePersistenceAsync,
        Func<CancellationToken, Task<MutePersistenceResult>> getMutePersistenceAsync,
        Func<CancellationToken, Task<IpcStatusData>> getStatusAsync,
        Func<string, IpcCommand?, CancellationToken, Task<UnifiedPlayerStateResult>> playerControlAsync,
        Action<string>? log = null,
        Action<string?>? recordError = null)
    {
        _navigateAsync = navigateAsync;
        _mediaControlAsync = mediaControlAsync;
        _getAudioStatusAsync = getAudioStatusAsync;
        _setVolumeAsync = setVolumeAsync;
        _setMutedAsync = setMutedAsync;
        _seekToAsync = seekToAsync;
        _inlineFullscreenAsync = inlineFullscreenAsync;
        _setMutePersistenceAsync = setMutePersistenceAsync;
        _getMutePersistenceAsync = getMutePersistenceAsync;
        _getStatusAsync = getStatusAsync;
        _playerControlAsync = playerControlAsync;
        _log = log ?? (_ => { });
        _recordError = recordError ?? (_ => { });
    }

    public async Task<string> HandleAsync(string requestJson, CancellationToken cancellationToken)
    {
        IpcCommand? request;
        try
        {
            request = JsonSerializer.Deserialize<IpcCommand>(requestJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"IPC invalid JSON: {ex.Message}");
            _recordError(ex.Message);
            _log($"CommandReceived: invalid-json; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Serialize(Fail(null, IpcConstants.InvalidJsonMessage, IpcConstants.ErrorCodeInvalidJson));
        }

        var command = request?.Command?.Trim().ToLowerInvariant();
        Debug.WriteLine($"IPC command received: {command ?? "(empty)"}");
        _log($"CommandReceived: {command ?? "(empty)"}");

        return command switch
        {
            IpcConstants.CommandPing => Serialize(Ping()),
            IpcConstants.CommandNavigate => Serialize(await NavigateAsync(request, cancellationToken)),
            IpcConstants.CommandGetStatus => Serialize(await GetStatusAsync(cancellationToken)),
            IpcConstants.CommandPlay
                or IpcConstants.CommandPause
                or IpcConstants.CommandToggleMute
                or IpcConstants.CommandSeekToStart => Serialize(await ControlMediaAsync(command, cancellationToken)),
            IpcConstants.CommandSeekTo => Serialize(await SeekToAsync(request, cancellationToken)),
            IpcConstants.CommandGetAudioStatus => Serialize(await GetAudioStatusAsync(cancellationToken)),
            IpcConstants.CommandSetVolume => Serialize(await SetVolumeAsync(request, cancellationToken)),
            IpcConstants.CommandSetMuted => Serialize(await SetMutedAsync(request, cancellationToken)),
            IpcConstants.CommandEnterInlineFullscreen
                or IpcConstants.CommandExitInlineFullscreen
                or IpcConstants.CommandToggleInlineFullscreen => Serialize(await ControlInlineFullscreenAsync(command, cancellationToken)),
            IpcConstants.CommandSetMutePersistence => Serialize(await SetMutePersistenceAsync(request, cancellationToken)),
            IpcConstants.CommandGetMutePersistence => Serialize(await GetMutePersistenceAsync(cancellationToken)),
            IpcConstants.CommandPlayerGetState
                or IpcConstants.CommandPlayerPlay
                or IpcConstants.CommandPlayerPause
                or IpcConstants.CommandPlayerStop
                or IpcConstants.CommandPlayerSeek
                or IpcConstants.CommandPlayerNext
                or IpcConstants.CommandPlayerPrevious
                or IpcConstants.CommandPlayerNextChapter
                or IpcConstants.CommandPlayerPreviousChapter
                or IpcConstants.CommandPlayerSetVolume
                or IpcConstants.CommandPlayerSetMuted
                or IpcConstants.CommandPlayerSetControlPolicy
                or IpcConstants.CommandPlayerClearControlPolicy => Serialize(await ControlPlayerAsync(command, request, cancellationToken)),
            _ => Serialize(Fail(command, IpcConstants.UnsupportedCommandMessage, IpcConstants.ErrorCodeUnsupportedCommand))
        };
    }

    private IpcResponse Ping()
    {
        _recordError(null);
        _log($"Command: {IpcConstants.CommandPing}; Result: Success; ResponseProcessId: {_processId}");
        return new IpcResponse
        {
            Success = true,
            Command = IpcConstants.CommandPing,
            ProcessId = _processId,
            Message = IpcConstants.PongMessage
        };
    }

    private async Task<IpcResponse> NavigateAsync(IpcCommand? request, CancellationToken cancellationToken)
    {
        if (!TryNormalizeUrl(request?.Url, out var normalizedUrl))
        {
            _recordError(IpcConstants.InvalidUrlMessage);
            _log($"Command: {IpcConstants.CommandNavigate}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidUrl}");
            return Fail(IpcConstants.CommandNavigate, IpcConstants.InvalidUrlMessage, IpcConstants.ErrorCodeInvalidUrl);
        }

        try
        {
            var navigated = await _navigateAsync(normalizedUrl, cancellationToken);
            _recordError(navigated ? null : IpcConstants.WebViewNotReadyMessage);
            _log(
                $"Command: {IpcConstants.CommandNavigate}; Result: {(navigated ? "Success" : "Failed")}; ResponseProcessId: {_processId}");
            return navigated
                ? new IpcResponse
                {
                    Success = true,
                    Command = IpcConstants.CommandNavigate,
                    ProcessId = _processId,
                    Message = IpcConstants.NavigateAcceptedMessage
                }
                : Fail(
                    IpcConstants.CommandNavigate,
                    IpcConstants.WebViewNotReadyMessage,
                    IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC navigate failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandNavigate}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandNavigate, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            _recordError(null);
            _log($"Command: {IpcConstants.CommandGetStatus}; Result: Success; ResponseProcessId: {_processId}");
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandGetStatus,
                ProcessId = _processId,
                Message = IpcConstants.StatusMessage,
                Data = await _getStatusAsync(cancellationToken)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC get-status failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandGetStatus}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandGetStatus, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> ControlInlineFullscreenAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inlineFullscreenAsync(command, cancellationToken);
            if (!result.Success)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeInlineFullscreenStateUnknown
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "枠内全画面操作に失敗しました。"
                    : result.Message;
                _recordError(message);
                _log($"Command: {command}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(command, message, errorCode, result);
            }

            _recordError(null);
            _log($"Command: {command}; Result: Success; ResponseProcessId: {_processId}");
            return new IpcResponse
            {
                Success = true,
                Command = command,
                ProcessId = _processId,
                Message = IpcConstants.InlineFullscreenAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC inline fullscreen rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC inline fullscreen failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> ControlMediaAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediaControlAsync(command, cancellationToken);
            if (!result.Success || !result.MediaFound)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeMediaOperationFailed
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? (string.IsNullOrWhiteSpace(result.OperationError)
                        ? IpcConstants.MediaNotFoundMessage
                        : result.OperationError)
                    : result.Message;
                _recordError(message);
                _log($"Command: {command}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(command, message, errorCode, result);
            }

            _recordError(null);
            _log($"Command: {command}; Result: Success; ResponseProcessId: {_processId}");
            return new IpcResponse
            {
                Success = true,
                Command = command,
                ProcessId = _processId,
                Message = IpcConstants.MediaControlAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC media control rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC media control failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> GetAudioStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _getAudioStatusAsync(cancellationToken);
            if (!result.Success || !result.MediaFound)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeAudioStatusUnavailable
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? IpcConstants.MediaNotFoundMessage
                    : result.Message;
                _recordError(message);
                _log($"Command: {IpcConstants.CommandGetAudioStatus}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(IpcConstants.CommandGetAudioStatus, message, errorCode, result);
            }

            _recordError(null);
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandGetAudioStatus,
                ProcessId = _processId,
                Message = IpcConstants.AudioStatusMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC audio status rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandGetAudioStatus}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandGetAudioStatus, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC audio status failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandGetAudioStatus}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandGetAudioStatus, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> SeekToAsync(IpcCommand? request, CancellationToken cancellationToken)
    {
        if (!TryReadPositionSeconds(request?.PositionSeconds, out var positionSeconds, out var validationMessage))
        {
            _recordError(validationMessage);
            _log($"Command: {IpcConstants.CommandSeekTo}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidParameter}");
            return Fail(IpcConstants.CommandSeekTo, validationMessage, IpcConstants.ErrorCodeInvalidParameter);
        }

        try
        {
            var result = await _seekToAsync(positionSeconds, cancellationToken);
            if (!result.Success || !result.MediaFound)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeUnknownError
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? IpcConstants.MediaNotFoundMessage
                    : result.Message;
                _recordError(message);
                _log($"Command: {IpcConstants.CommandSeekTo}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(IpcConstants.CommandSeekTo, message, errorCode, result);
            }

            _recordError(null);
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandSeekTo,
                ProcessId = _processId,
                Message = IpcConstants.SeekAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC seek-to rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSeekTo}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSeekTo, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC seek-to failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSeekTo}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSeekTo, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> SetVolumeAsync(IpcCommand? request, CancellationToken cancellationToken)
    {
        if (!TryReadVolumePercent(request?.VolumePercent, out var volumePercent, out var validationMessage))
        {
            _recordError(validationMessage);
            _log($"Command: {IpcConstants.CommandSetVolume}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidVolume}");
            return Fail(IpcConstants.CommandSetVolume, validationMessage, IpcConstants.ErrorCodeInvalidVolume);
        }

        try
        {
            var result = await _setVolumeAsync(volumePercent, cancellationToken);
            if (!result.Success || !result.MediaFound)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeVolumeSetFailed
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? IpcConstants.MediaNotFoundMessage
                    : result.Message;
                _recordError(message);
                _log($"Command: {IpcConstants.CommandSetVolume}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(IpcConstants.CommandSetVolume, message, errorCode, result);
            }

            _recordError(null);
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandSetVolume,
                ProcessId = _processId,
                Message = IpcConstants.VolumeAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC set-volume rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetVolume}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSetVolume, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC set-volume failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetVolume}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSetVolume, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> SetMutedAsync(IpcCommand? request, CancellationToken cancellationToken)
    {
        if (!TryReadMuted(request?.Muted, out var muted, out var validationMessage))
        {
            _recordError(validationMessage);
            _log($"Command: {IpcConstants.CommandSetMuted}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidMutedValue}");
            return Fail(IpcConstants.CommandSetMuted, validationMessage, IpcConstants.ErrorCodeInvalidMutedValue);
        }

        try
        {
            var result = await _setMutedAsync(muted, cancellationToken);
            if (!result.Success || !result.MediaFound)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeMuteSetFailed
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? IpcConstants.MediaNotFoundMessage
                    : result.Message;
                _recordError(message);
                _log($"Command: {IpcConstants.CommandSetMuted}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(IpcConstants.CommandSetMuted, message, errorCode, result);
            }

            _recordError(null);
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandSetMuted,
                ProcessId = _processId,
                Message = IpcConstants.MutedAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC set-muted rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetMuted}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSetMuted, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC set-muted failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetMuted}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSetMuted, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> SetMutePersistenceAsync(IpcCommand? request, CancellationToken cancellationToken)
    {
        var enabled = request?.Enabled ?? request?.MutePersistenceEnabled ?? request?.Value;
        if (!enabled.HasValue)
        {
            _recordError("Mute persistence enabled value is required.");
            _log($"Command: {IpcConstants.CommandSetMutePersistence}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeMutePersistenceSetFailed}");
            return Fail(
                IpcConstants.CommandSetMutePersistence,
                "Mute persistence enabled value is required.",
                IpcConstants.ErrorCodeMutePersistenceSetFailed);
        }

        try
        {
            var result = await _setMutePersistenceAsync(enabled, cancellationToken);
            if (!result.Success)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeMutePersistenceSetFailed
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "ミュート継続設定に失敗しました。"
                    : result.Message;
                _recordError(message);
                _log($"Command: {IpcConstants.CommandSetMutePersistence}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(IpcConstants.CommandSetMutePersistence, message, errorCode, result);
            }

            _recordError(null);
            _log($"Command: {IpcConstants.CommandSetMutePersistence}; Result: Success; ResponseProcessId: {_processId}");
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandSetMutePersistence,
                ProcessId = _processId,
                Message = IpcConstants.MutePersistenceAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC mute persistence rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetMutePersistence}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(
                IpcConstants.CommandSetMutePersistence,
                IpcConstants.WebViewNotReadyMessage,
                IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC mute persistence failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandSetMutePersistence}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandSetMutePersistence, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> ControlPlayerAsync(string command, IpcCommand? request, CancellationToken cancellationToken)
    {
        if (command is IpcConstants.CommandPlayerSeek
            && !TryReadPositionSeconds(request?.PositionSeconds, out _, out var seekValidationMessage))
        {
            _recordError(seekValidationMessage);
            _log($"Command: {command}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidParameter}");
            return Fail(command, seekValidationMessage, IpcConstants.ErrorCodeInvalidParameter);
        }

        if (command == IpcConstants.CommandPlayerSetVolume
            && !TryReadVolumePercent(request?.VolumePercent, out _, out var requiredVolumeValidationMessage))
        {
            _recordError(requiredVolumeValidationMessage);
            _log($"Command: {command}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidVolume}; ParameterPresent={request?.VolumePercent.HasValue == true}");
            return Fail(command, requiredVolumeValidationMessage, IpcConstants.ErrorCodeInvalidVolume);
        }

        if (command == IpcConstants.CommandPlayerSetMuted
            && !TryReadMuted(request?.Muted, out _, out var requiredMutedValidationMessage))
        {
            _recordError(requiredMutedValidationMessage);
            _log($"Command: {command}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidMutedValue}; ParameterPresent={request?.Muted.HasValue == true}");
            return Fail(command, requiredMutedValidationMessage, IpcConstants.ErrorCodeInvalidMutedValue);
        }

        if (command == IpcConstants.CommandPlayerSetControlPolicy
            && request?.VolumePercent.HasValue == true
            && !TryReadVolumePercent(request.VolumePercent, out _, out var volumeValidationMessage))
        {
            _recordError(volumeValidationMessage);
            _log($"Command: {command}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidVolume}");
            return Fail(command, volumeValidationMessage, IpcConstants.ErrorCodeInvalidVolume);
        }

        if (command == IpcConstants.CommandPlayerSetControlPolicy
            && request?.Muted.HasValue == true
            && !TryReadMuted(request.Muted, out _, out var mutedValidationMessage))
        {
            _recordError(mutedValidationMessage);
            _log($"Command: {command}; Result: Failed; ErrorCode: {IpcConstants.ErrorCodeInvalidMutedValue}");
            return Fail(command, mutedValidationMessage, IpcConstants.ErrorCodeInvalidMutedValue);
        }

        try
        {
            var result = await _playerControlAsync(command, request, cancellationToken);
            if (!result.Success || !result.MediaFound && command != IpcConstants.CommandPlayerClearControlPolicy)
            {
                var errorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                    ? IpcConstants.ErrorCodeMediaOperationFailed
                    : result.ErrorCode;
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? IpcConstants.MediaNotFoundMessage
                    : result.Message;
                _recordError(message);
                _log($"Command: {command}; Result: Failed; ErrorCode: {errorCode}");
                return Fail(command, message, errorCode, result);
            }

            _recordError(null);
            _log($"Command: {command}; Result: Success; ResponseProcessId: {_processId}");
            return new IpcResponse
            {
                Success = true,
                Command = command,
                ProcessId = _processId,
                Message = command == IpcConstants.CommandPlayerGetState
                    ? IpcConstants.PlayerStateMessage
                    : IpcConstants.PlayerControlAcceptedMessage,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC player control rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.WebViewNotReadyMessage, IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC player control failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {command}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(command, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private async Task<IpcResponse> GetMutePersistenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _getMutePersistenceAsync(cancellationToken);
            _recordError(result.Success ? null : result.Message);
            _log($"Command: {IpcConstants.CommandGetMutePersistence}; Result: {(result.Success ? "Success" : "Failed")}; ResponseProcessId: {_processId}; ErrorCode: {result.ErrorCode ?? string.Empty}");
            return new IpcResponse
            {
                Success = result.Success,
                Command = IpcConstants.CommandGetMutePersistence,
                ProcessId = _processId,
                Message = IpcConstants.MutePersistenceStatusMessage,
                ErrorCode = result.Success ? null : result.ErrorCode,
                Data = result
            };
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"IPC mute persistence status rejected: {ex.Message}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandGetMutePersistence}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(
                IpcConstants.CommandGetMutePersistence,
                IpcConstants.WebViewNotReadyMessage,
                IpcConstants.ErrorCodeWebViewNotReady);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC mute persistence status failed: {ex}");
            _recordError(ex.Message);
            _log($"Command: {IpcConstants.CommandGetMutePersistence}; Result: Failed; ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            return Fail(IpcConstants.CommandGetMutePersistence, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private IpcResponse Fail(string? command, string message, string errorCode, object? data = null)
    {
        return new IpcResponse
        {
            Success = false,
            Command = command,
            ProcessId = _processId,
            Message = message,
            ErrorCode = errorCode,
            Data = data
        };
    }

    private static bool TryNormalizeUrl(string? value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > IpcConstants.MaxUrlLength)
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    private static bool TryReadVolumePercent(JsonElement? value, out int volumePercent, out string message)
    {
        volumePercent = 0;
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            message = "volumePercent is required.";
            return false;
        }

        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetInt32(out volumePercent))
        {
            message = "volumePercent must be an integer from 0 to 100.";
            return false;
        }

        if (volumePercent is < 0 or > 100)
        {
            message = "volumePercent must be between 0 and 100.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryReadMuted(JsonElement? value, out bool muted, out string message)
    {
        muted = false;
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            message = "muted is required.";
            return false;
        }

        if (value.Value.ValueKind == JsonValueKind.True)
        {
            muted = true;
            message = string.Empty;
            return true;
        }

        if (value.Value.ValueKind == JsonValueKind.False)
        {
            muted = false;
            message = string.Empty;
            return true;
        }

        message = "muted must be true or false.";
        return false;
    }

    private static bool TryReadPositionSeconds(JsonElement? value, out double positionSeconds, out string message)
    {
        positionSeconds = 0;
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            message = "positionSeconds is required.";
            return false;
        }

        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetDouble(out positionSeconds))
        {
            message = "positionSeconds must be a finite number.";
            return false;
        }

        if (!double.IsFinite(positionSeconds) || positionSeconds < 0)
        {
            message = "positionSeconds must be a finite number greater than or equal to 0.";
            return false;
        }

        if (positionSeconds > 31_536_000)
        {
            message = "positionSeconds is too large.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string Serialize(IpcResponse response)
    {
        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
