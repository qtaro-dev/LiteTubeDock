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
    private readonly Func<IpcStatusData> _getStatus;

    public IpcCommandHandler(
        Func<string, CancellationToken, Task<bool>> navigateAsync,
        Func<IpcStatusData> getStatus)
    {
        _navigateAsync = navigateAsync;
        _getStatus = getStatus;
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
            return Serialize(Fail(null, IpcConstants.InvalidJsonMessage, IpcConstants.ErrorCodeInvalidJson));
        }

        var command = request?.Command?.Trim().ToLowerInvariant();
        Debug.WriteLine($"IPC command received: {command ?? "(empty)"}");

        return command switch
        {
            IpcConstants.CommandPing => Serialize(Ping()),
            IpcConstants.CommandNavigate => Serialize(await NavigateAsync(request, cancellationToken)),
            IpcConstants.CommandGetStatus => Serialize(GetStatus()),
            _ => Serialize(Fail(command, IpcConstants.UnsupportedCommandMessage, IpcConstants.ErrorCodeUnsupportedCommand))
        };
    }

    private IpcResponse Ping()
    {
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
            return Fail(IpcConstants.CommandNavigate, IpcConstants.InvalidUrlMessage, IpcConstants.ErrorCodeInvalidUrl);
        }

        try
        {
            var navigated = await _navigateAsync(normalizedUrl, cancellationToken);
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
            return Fail(IpcConstants.CommandNavigate, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private IpcResponse GetStatus()
    {
        try
        {
            return new IpcResponse
            {
                Success = true,
                Command = IpcConstants.CommandGetStatus,
                ProcessId = _processId,
                Message = IpcConstants.StatusMessage,
                Data = _getStatus()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC get-status failed: {ex}");
            return Fail(IpcConstants.CommandGetStatus, IpcConstants.InternalErrorMessage, IpcConstants.ErrorCodeInternalError);
        }
    }

    private IpcResponse Fail(string? command, string message, string errorCode)
    {
        return new IpcResponse
        {
            Success = false,
            Command = command,
            ProcessId = _processId,
            Message = message,
            ErrorCode = errorCode
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

    private static string Serialize(IpcResponse response)
    {
        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
