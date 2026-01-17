using System.IO.Pipes;
using EnoUnityLoader.Ipc.Messages;

namespace EnoUnityLoader.Ipc;

/// <summary>
/// IPC Client - runs in the loader (injected into game).
/// Connects to the UI application.
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private const string PipeName = "EnoUnityLoader_IPC";
    private const int ConnectionTimeout = 5000;

    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _isConnected;

    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<IpcMessage>? OnMessageReceived;
    public event Action<Exception>? OnError;

    public bool IsConnected => _isConnected;

    /// <summary>
    /// Connects to the IPC server (UI application).
    /// </summary>
    public async Task<bool> ConnectAsync(int timeoutMs = ConnectionTimeout, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _pipeClient = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );

        try
        {
            await _pipeClient.ConnectAsync(timeoutMs, cancellationToken);
            _isConnected = true;

            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };

            OnConnected?.Invoke();

            // Start reading messages in background
            _readTask = ReadMessagesAsync(_cts.Token);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
            return false;
        }
    }

    private async Task ReadMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _isConnected = false;
                    OnDisconnected?.Invoke();
                    break;
                }

                try
                {
                    var message = IpcMessage.Deserialize(line);
                    if (message != null)
                    {
                        OnMessageReceived?.Invoke(message);
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(new InvalidOperationException($"Failed to deserialize message: {line}", ex));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (IOException)
        {
            // Pipe broken - server disconnected
            _isConnected = false;
            OnDisconnected?.Invoke();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Sends a message to the server.
    /// </summary>
    public async Task SendAsync(IpcMessage message)
    {
        if (!_isConnected || _writer == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        var json = message.Serialize();
        await _writer.WriteLineAsync(json);
    }

    /// <summary>
    /// Sends a message if connected, otherwise silently ignores.
    /// </summary>
    public async Task TrySendAsync(IpcMessage message)
    {
        if (!_isConnected || _writer == null) return;

        try
        {
            var json = message.Serialize();
            await _writer.WriteLineAsync(json);
        }
        catch
        {
            // Ignore send errors
        }
    }

    /// <summary>
    /// Sends a progress update.
    /// </summary>
    public Task SendProgressAsync(string stage, string description, double progress = -1, int? current = null, int? total = null)
    {
        return TrySendAsync(new ProgressMessage
        {
            Stage = stage,
            Description = description,
            Progress = progress,
            CurrentItem = current,
            TotalItems = total
        });
    }

    /// <summary>
    /// Sends a status update.
    /// </summary>
    public Task SendStatusAsync(LoaderStatus status, string? message = null)
    {
        return TrySendAsync(new StatusMessage
        {
            Status = status,
            Message = message
        });
    }

    /// <summary>
    /// Sends a log message.
    /// </summary>
    public Task SendLogAsync(LogLevel level, string source, string message)
    {
        return TrySendAsync(new LogMessage
        {
            Level = level,
            Source = source,
            Message = message
        });
    }

    public async ValueTask DisposeAsync()
    {
        _isConnected = false;
        _cts?.Cancel();

        if (_readTask != null)
        {
            try
            {
                await _readTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        _reader?.Dispose();
        _writer?.Dispose();

        if (_pipeClient != null)
        {
            await _pipeClient.DisposeAsync();
        }

        _cts?.Dispose();
    }
}
