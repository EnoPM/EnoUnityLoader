using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using EnoUnityLoader.Ipc;
using EnoUnityLoader.Ipc.Messages;

namespace EnoUnityLoader.Ui.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IpcServer _ipcServer;
    private readonly CancellationTokenSource _cts = new();

    public MainViewModel()
    {
        _ipcServer = new IpcServer();

        _ipcServer.OnConnected += HandleConnected;
        _ipcServer.OnDisconnected += HandleDisconnected;
        _ipcServer.OnMessageReceived += HandleMessage;
        _ipcServer.OnError += HandleError;

        Logs = [];
    }

    public string Status
    {
        get;
        private set => SetField(ref field, value);
    } = "Waiting for loader...";

    public string Stage
    {
        get;
        private set => SetField(ref field, value);
    } = "";

    public string Description
    {
        get;
        private set => SetField(ref field, value);
    } = "";

    public double Progress
    {
        get;
        private set => SetField(ref field, value);
    } = 0;

    public string ProgressText
    {
        get;
        private set => SetField(ref field, value);
    } = "";

    public bool IsIndeterminate
    {
        get;
        private set => SetField(ref field, value);
    } = true;

    public bool IsConnected
    {
        get;
        private set
        {
            if (SetField(ref field, value))
            {
                OnPropertyChanged(nameof(LogoOpacity));
            }
        }
    }

    public double LogoOpacity => IsConnected ? 1.0 : 0.5;

    public ObservableCollection<LogEntry> Logs { get; }

    public async Task StartAsync()
    {
        await _ipcServer.StartAsync();

        // Start monitoring the game process if we have a PID
        if (Program.GameProcessId.HasValue)
        {
            _ = MonitorGameProcessAsync(Program.GameProcessId.Value, _cts.Token);
        }
    }

    private async Task MonitorGameProcessAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken);

            // Game has exited, close the UI
            RunOnUiThread(() =>
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            });
        }
        catch (ArgumentException)
        {
            // Process doesn't exist (already exited), close UI
            RunOnUiThread(() =>
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Monitoring was cancelled
        }
        catch
        {
            // Ignore other errors
        }
    }

    private void HandleConnected()
    {
        RunOnUiThread(() =>
        {
            IsConnected = true;
            Status = "Connected to loader";
        });
    }

    private void HandleDisconnected()
    {
        RunOnUiThread(() =>
        {
            IsConnected = false;
            Status = "Loader disconnected";
        });
    }

    private void HandleMessage(IpcMessage message)
    {
        RunOnUiThread(() =>
        {
            switch (message)
            {
                case ProgressMessage progress:
                    HandleProgress(progress);
                    break;

                case StatusMessage status:
                    HandleStatus(status);
                    break;

                case LogMessage log:
                    HandleLog(log);
                    break;

                case ReadyMessage ready:
                    HandleReady(ready);
                    break;
            }
        });
    }

    private void HandleProgress(ProgressMessage msg)
    {
        Stage = msg.Stage;
        Description = msg.Description;

        if (msg.Progress < 0)
        {
            IsIndeterminate = true;
            Progress = 0;
            ProgressText = "";
        }
        else
        {
            IsIndeterminate = false;
            Progress = msg.Progress * 100;

            if (msg.CurrentItem.HasValue && msg.TotalItems.HasValue)
            {
                ProgressText = $"{msg.CurrentItem}/{msg.TotalItems}";
            }
            else
            {
                ProgressText = $"{msg.Progress:P0}";
            }
        }
    }

    private void HandleStatus(StatusMessage msg)
    {
        Status = msg.Status switch
        {
            LoaderStatus.Initializing => "Initializing...",
            LoaderStatus.DownloadingLibraries => "Downloading Unity libraries...",
            LoaderStatus.GeneratingInterop => "Generating interop assemblies...",
            LoaderStatus.LoadingMods => "Loading mods...",
            LoaderStatus.Ready => "Ready!",
            LoaderStatus.Error => $"Error: {msg.Message}",
            _ => msg.Message ?? "Unknown status"
        };
    }

    private void HandleLog(LogMessage msg)
    {
        Logs.Add(new LogEntry
        {
            Timestamp = msg.Timestamp,
            Level = msg.Level,
            Source = msg.Source,
            Message = msg.Message
        });

        // Keep last 1000 logs
        while (Logs.Count > 1000)
        {
            Logs.RemoveAt(0);
        }
    }

    private void HandleReady(ReadyMessage msg)
    {
        if (msg.Success)
        {
            Status = "Loading complete!";
            Stage = "";
            Description = "Game is starting...";
            IsIndeterminate = false;
            Progress = 100;
        }
        else
        {
            Status = "Loading failed";
            Description = msg.ErrorMessage ?? "Unknown error";
        }
    }

    private void HandleError(Exception ex)
    {
        RunOnUiThread(() =>
        {
            Logs.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Source = "IPC",
                Message = ex.Message
            });
        });
    }

    private static void RunOnUiThread(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        await _ipcServer.DisposeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
}
