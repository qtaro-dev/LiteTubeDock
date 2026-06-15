using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using LiteTubeDock.Constants;

namespace LiteTubeDock.Services;

public sealed class NamedPipeServerService : IDisposable
{
    private readonly Func<string, CancellationToken, Task<string>> _commandHandler;
    private readonly Action<string> _log;
    private readonly Action<string?> _recordError;
    private readonly CancellationTokenSource _stopTokenSource = new();
    private Task? _serverTask;
    private string _pipeName = string.Empty;
    private bool _disposed;

    public NamedPipeServerService(
        Func<string, CancellationToken, Task<string>> commandHandler,
        Action<string>? log = null,
        Action<string?>? recordError = null)
    {
        _commandHandler = commandHandler;
        _log = log ?? (_ => { });
        _recordError = recordError ?? (_ => { });
    }

    public void Start(string pipeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_serverTask is not null)
        {
            return;
        }

        _pipeName = pipeName;
        _log($"Start requested. PipeName: {_pipeName}");
        _serverTask = Task.Run(() => RunServerAsync(_stopTokenSource.Token));
        Debug.WriteLine($"Named pipe server started: {_pipeName}");
        _log("Start result: Success");
    }

    public async Task StopAsync()
    {
        if (_serverTask is null)
        {
            return;
        }

        try
        {
            _stopTokenSource.Cancel();
            await _serverTask.WaitAsync(TimeSpan.FromMilliseconds(500));
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Named pipe server stop failed: {ex}");
            _recordError(ex.Message);
            _log($"Server stop failed. ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
        }
        finally
        {
            Debug.WriteLine($"Named pipe server stopped: {_pipeName}");
            _log("ServerStopped");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopTokenSource.Cancel();
        _stopTokenSource.Dispose();
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
                _log("WaitingForConnection");
                await pipe.WaitForConnectionAsync(cancellationToken);
                Debug.WriteLine($"Named pipe connection accepted: {_pipeName}");
                _log("ClientConnected");

                using var commandTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                commandTokenSource.CancelAfter(IpcConstants.PipeCommandTimeoutMilliseconds);

                _log("ReadStarted");
                var request = await ReadRequestAsync(pipe, commandTokenSource.Token);
                var response = await _commandHandler(request, commandTokenSource.Token);
                await WriteResponseAsync(pipe, response, commandTokenSource.Token);
                _log("ResponseSent");
                _log("ClientDisconnected");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log("Cancelled");
                break;
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"Named pipe command timed out: {ex.Message}");
                _recordError(ex.Message);
                _log($"ConnectionTimedOut. ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Named pipe server error: {ex}");
                _recordError(ex.Message);
                _log($"Server error. ExceptionType: {ex.GetType().Name}; Message: {ex.Message}");
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        _log("CurrentUserOnly: True");
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    private static async Task<string> ReadRequestAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1024];
        var exceededMaxBytes = false;

        do
        {
            var bytesRead = await pipe.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (!exceededMaxBytes)
            {
                if (memory.Length + bytesRead > IpcConstants.MaxCommandBytes)
                {
                    exceededMaxBytes = true;
                }
                else
                {
                    memory.Write(buffer, 0, bytesRead);
                }
            }
        }
        while (!pipe.IsMessageComplete);

        if (exceededMaxBytes)
        {
            return "{";
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static async Task WriteResponseAsync(
        NamedPipeServerStream pipe,
        string response,
        CancellationToken cancellationToken)
    {
        var responseBytes = Encoding.UTF8.GetBytes(response);
        await pipe.WriteAsync(responseBytes, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
        pipe.WaitForPipeDrain();
    }
}
