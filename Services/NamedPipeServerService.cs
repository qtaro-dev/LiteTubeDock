using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using LiteTubeDock.Constants;

namespace LiteTubeDock.Services;

public sealed class NamedPipeServerService : IDisposable
{
    private readonly Func<string, CancellationToken, Task<string>> _commandHandler;
    private readonly CancellationTokenSource _stopTokenSource = new();
    private Task? _serverTask;
    private string _pipeName = string.Empty;
    private bool _disposed;

    public NamedPipeServerService(Func<string, CancellationToken, Task<string>> commandHandler)
    {
        _commandHandler = commandHandler;
    }

    public void Start(string pipeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_serverTask is not null)
        {
            return;
        }

        _pipeName = pipeName;
        _serverTask = Task.Run(() => RunServerAsync(_stopTokenSource.Token));
        Debug.WriteLine($"Named pipe server started: {_pipeName}");
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
        }
        finally
        {
            Debug.WriteLine($"Named pipe server stopped: {_pipeName}");
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
                await pipe.WaitForConnectionAsync(cancellationToken);
                Debug.WriteLine($"Named pipe connection accepted: {_pipeName}");

                var request = await ReadRequestAsync(pipe, cancellationToken);
                var response = await _commandHandler(request, cancellationToken);
                await WriteResponseAsync(pipe, response, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Named pipe server error: {ex}");
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);
    }

    private static async Task<string> ReadRequestAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1024];

        do
        {
            var bytesRead = await pipe.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            memory.Write(buffer, 0, bytesRead);
            if (memory.Length > IpcConstants.MaxCommandBytes)
            {
                break;
            }
        }
        while (!pipe.IsMessageComplete);

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
