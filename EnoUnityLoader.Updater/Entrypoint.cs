using System.Diagnostics;
using System.Runtime.InteropServices;
using EnoUnityLoader.Updater;

// ReSharper disable once CheckNamespace
namespace Doorstop;

/// <summary>
/// Doorstop entry point for EnoUnityLoader with auto-update support.
/// </summary>
internal static class Entrypoint
{
    /// <summary>
    /// The main entrypoint called from Doorstop.
    /// </summary>
    public static void Start()
    {
        var silentExceptionLog = $"updater_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
        Mutex? mutex = null;

        try
        {
            EnvVars.Load();

            silentExceptionLog = Path.Combine(
                Path.GetDirectoryName(EnvVars.DoorstopProcessPath) ?? ".",
                silentExceptionLog);

            var mutexId = HashStrings(
                Process.GetCurrentProcess().ProcessName,
                EnvVars.DoorstopProcessPath ?? "",
                typeof(Entrypoint).FullName ?? "");

            mutex = new Mutex(false, $"Global\\{mutexId}");
            mutex.WaitOne();

            RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            File.WriteAllText(silentExceptionLog, ex.ToString());

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    MessageBox("Failed to start EnoUnityLoader Updater:\n\n" + ex.Message, "EnoUnityLoader");
                }
            }
            catch
            {
                // Ignore
            }

            Environment.Exit(1);
        }
        finally
        {
            mutex?.ReleaseMutex();
        }
    }

    private static async Task RunAsync()
    {
        var coreDirectory = EnvVars.GetCoreDirectory();
        var gameProcessId = Process.GetCurrentProcess().Id;

        await using var orchestrator = new UpdateOrchestrator(coreDirectory);
        await orchestrator.RunAsync(gameProcessId);
    }

    private static string HashStrings(params string[] strings)
    {
        var hash = 17;
        foreach (var s in strings)
        {
            foreach (var c in s)
            {
                hash = hash * 31 + c;
            }
        }
        return hash.ToString("X8");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static void MessageBox(string text, string caption)
    {
        MessageBoxW(IntPtr.Zero, text, caption, 0x10); // MB_ICONERROR
    }
}
