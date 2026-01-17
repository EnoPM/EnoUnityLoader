using System.IO.Pipes;
using EnoUnityLoader.Ipc.Messages;

namespace EnoUnityLoader.Ipc;

/// <summary>
/// IPC Server - runs in the UI application.
/// Listens for connections from the loader.
/// </summary>
public sealed class IpcServer : IAsyncDisposable
{
    private const string PipeName = "EnoUnityLoader_IPC";
    private const int MaxMessageSize = 1024 * 64; // 64KB

    private NamedPipeServerStream? _pipeServer;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isConnected;

    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<IpcMessage>? OnMessageReceived;
    public event Action<Exception>? OnError;

    public bool IsConnected => _isConnected;

    /// <summary>
    /// Starts listening for incoming connections.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _pipeServer = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );

        _listenTask = ListenAsync(_cts.Token);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_pipeServer == null) break;

                // Wait for connection
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                _isConnected = true;

                _reader = new StreamReader(_pipeServer);
                _writer = new StreamWriter(_pipeServer) { AutoFlush = true };

                OnConnected?.Invoke();

                // Read messages until disconnection
                await ReadMessagesAsync(cancellationToken);

                _isConnected = false;
                OnDisconnected?.Invoke();

                // Disconnect and wait for new connection
                if (_pipeServer.IsConnected)
                {
                    _pipeServer.Disconnect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    private async Task ReadMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null) break; // Disconnected

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
        catch (IOException)
        {
            // Pipe broken - client disconnected
        }
    }

    /// <summary>
    /// Sends a message to the connected client.
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

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_listenTask != null)
        {
            try
            {
                await _listenTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        _reader?.Dispose();
        _writer?.Dispose();

        if (_pipeServer != null)
        {
            await _pipeServer.DisposeAsync();
        }

        _cts?.Dispose();
    }
}
