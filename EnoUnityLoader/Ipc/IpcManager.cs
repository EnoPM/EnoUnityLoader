using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EnoUnityLoader.Ipc.Messages;
using EnoUnityLoader.Logging;
using LogLevel = EnoUnityLoader.Logging.LogLevel;

namespace EnoUnityLoader.Ipc;

/// <summary>
/// Manages IPC communication between the loader and the UI application.
/// </summary>
public static class IpcManager
{
    private static IpcClient? _client;
    private static bool _isInitialized;

    /// <summary>
    /// Gets whether the IPC client is connected to the UI.
    /// </summary>
    public static bool IsConnected => _client?.IsConnected ?? false;

    /// <summary>
    /// Initializes the IPC manager: launches the UI and connects to it.
    /// </summary>
    /// <param name="loaderDirectory">Directory where the loader is installed.</param>
    /// <returns>True if successfully connected to UI.</returns>
    public static async Task<bool> InitializeAsync(string loaderDirectory)
    {
        if (_isInitialized) return IsConnected;
        _isInitialized = true;

        try
        {
            // Launch the UI application
            var gameProcessId = Process.GetCurrentProcess().Id;
            var launched = UiLauncher.LaunchUi(loaderDirectory, gameProcessId);

            if (!launched)
            {
                Logger.Log(LogLevel.Warning, "Failed to launch UI application");
                return false;
            }

            // Give the UI a moment to start the IPC server
            await Task.Delay(500);

            // Create and connect the client
            _client = new IpcClient();
            _client.OnError += ex => Logger.Log(LogLevel.Error, $"IPC Error: {ex.Message}");
            _client.OnDisconnected += () => Logger.Log(LogLevel.Info, "Disconnected from UI");

            // Try to connect with retries
            for (var i = 0; i < 5; i++)
            {
                if (await _client.ConnectAsync(2000))
                {
                    Logger.Log(LogLevel.Info, "Connected to UI application");
                    return true;
                }

                await Task.Delay(500);
            }

            Logger.Log(LogLevel.Warning, "Failed to connect to UI application");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, $"Failed to initialize IPC: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends a progress update to the UI.
    /// </summary>
    public static void SendProgress(string stage, string description, double progress = -1, int? current = null, int? total = null)
    {
        if (_client == null || !_client.IsConnected) return;
        _ = _client.SendProgressAsync(stage, description, progress, current, total);
    }

    /// <summary>
    /// Sends a status update to the UI.
    /// </summary>
    public static void SendStatus(LoaderStatus status, string? message = null)
    {
        if (_client == null || !_client.IsConnected) return;
        _ = _client.SendStatusAsync(status, message);
    }

    /// <summary>
    /// Sends a log message to the UI.
    /// </summary>
    public static void SendLog(Ipc.Messages.LogLevel level, string source, string message)
    {
        if (_client == null || !_client.IsConnected) return;
        _ = _client.SendLogAsync(level, source, message);
    }

    /// <summary>
    /// Sends the ready message to the UI.
    /// </summary>
    public static void SendReady(bool success, string? errorMessage = null)
    {
        if (_client == null || !_client.IsConnected) return;
        _ = _client.TrySendAsync(new ReadyMessage
        {
            Success = success,
            ErrorMessage = errorMessage
        });
    }

    /// <summary>
    /// Closes the UI application.
    /// </summary>
    public static void CloseUi()
    {
        UiLauncher.CloseUi();
    }

    /// <summary>
    /// Closes the UI application after a delay.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before closing.</param>
    public static void CloseUiDelayed(int delayMs)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            UiLauncher.CloseUi();
        });
    }

    /// <summary>
    /// Disposes the IPC client.
    /// </summary>
    public static async Task DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _isInitialized = false;
    }
}
