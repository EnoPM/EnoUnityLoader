using System.Diagnostics;

namespace EnoUnityLoader.Ipc;

/// <summary>
/// Helper to launch the UI application from the loader.
/// </summary>
public static class UiLauncher
{
    private const string UiExecutableName = "EnoUnityLoader.Ui.exe";

    private static Process? _uiProcess;

    /// <summary>
    /// Gets the last error message if LaunchUi failed.
    /// </summary>
    public static string? LastError { get; private set; }

    /// <summary>
    /// Launches the UI application if not already running.
    /// </summary>
    /// <param name="loaderDirectory">Directory where the loader is installed (core folder).</param>
    /// <param name="gameProcessId">Process ID of the game (passed to UI for reference).</param>
    /// <returns>True if launched or already running.</returns>
    public static bool LaunchUi(string loaderDirectory, int? gameProcessId = null)
    {
        LastError = null;

        // Check if already running
        if (_uiProcess != null && !_uiProcess.HasExited)
        {
            return true;
        }

        // Find UI executable
        var uiPath = Path.Combine(loaderDirectory, UiExecutableName);
        if (!File.Exists(uiPath))
        {
            // Try in a 'ui' subdirectory
            uiPath = Path.Combine(loaderDirectory, "ui", UiExecutableName);
        }

        if (!File.Exists(uiPath))
        {
            LastError = $"UI executable not found. Searched: {Path.Combine(loaderDirectory, UiExecutableName)} and {Path.Combine(loaderDirectory, "ui", UiExecutableName)}";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = uiPath,
                WorkingDirectory = Path.GetDirectoryName(uiPath),
                UseShellExecute = true
            };

            if (gameProcessId.HasValue)
            {
                startInfo.Arguments = $"--game-pid {gameProcessId.Value}";
            }

            _uiProcess = Process.Start(startInfo);
            if (_uiProcess == null)
            {
                LastError = "Process.Start returned null";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start UI process: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Checks if the UI process is still running.
    /// </summary>
    public static bool IsUiRunning()
    {
        return _uiProcess != null && !_uiProcess.HasExited;
    }

    /// <summary>
    /// Closes the UI application gracefully.
    /// </summary>
    public static void CloseUi()
    {
        if (_uiProcess == null || _uiProcess.HasExited) return;

        try
        {
            _uiProcess.CloseMainWindow();
            if (!_uiProcess.WaitForExit(3000))
            {
                _uiProcess.Kill();
            }
        }
        catch
        {
            // Ignore errors when closing
        }
    }
}
