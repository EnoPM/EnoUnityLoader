using System.Reflection;
using EnoUnityLoader.Ipc;
using EnoUnityLoader.Updater.GitHub;
using EnoUnityLoader.Updater.Mods;

namespace EnoUnityLoader.Updater;

/// <summary>
/// Orchestrates the update check, download, and loader initialization process.
/// </summary>
internal sealed class UpdateOrchestrator : IAsyncDisposable
{
    private const string LoaderAssemblyName = "EnoUnityLoader.dll";
    private const string LoaderEntrypointNamespace = "EnoUnityLoader.Il2Cpp";
    private const string LoaderEntrypointClass = "UnityPreloadRunner";
    private const string LoaderEntrypointMethod = "PreloaderMain";

    private readonly string _coreDirectory;
    private IpcClient? _ipcClient;

    public UpdateOrchestrator(string coreDirectory)
    {
        _coreDirectory = coreDirectory;
    }

    /// <summary>
    /// Runs the complete update and load process.
    /// </summary>
    public async Task RunAsync(int gameProcessId)
    {
        await LaunchAndConnectUiAsync(gameProcessId);

        try
        {
            await CheckAndApplyUpdatesAsync();
            await CheckAndUpdateModsAsync();
            LoadAndRunLoader();
        }
        finally
        {
            await DisconnectUiAsync();
        }
    }

    private async Task LaunchAndConnectUiAsync(int gameProcessId)
    {
        var uiLaunched = UiLauncher.LaunchUi(_coreDirectory, gameProcessId);

        if (!uiLaunched)
            return;

        // Give the UI time to start
        await Task.Delay(500);

        // Connect to the UI
        _ipcClient = new IpcClient();
        try
        {
            await _ipcClient.ConnectAsync(2000);
        }
        catch
        {
            // Continue without UI connection
            _ipcClient = null;
        }
    }

    private async Task DisconnectUiAsync()
    {
        var client = _ipcClient;
        _ipcClient = null;

        if (client != null)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch
            {
                // Ignore dispose errors - pipe may already be closed
            }
        }
    }

    private async Task CheckAndApplyUpdatesAsync()
    {
        SendProgress("Checking for updates", "Contacting GitHub...");

        using var checker = new UpdateChecker();

        GitHubRelease? update;
        try
        {
            update = await checker.CheckForUpdateAsync();
        }
        catch
        {
            // Network error - continue without update
            return;
        }

        if (update == null)
        {
            SendProgress("Up to date", $"Version {checker.GetCurrentVersion()}");
            await Task.Delay(300);
            return;
        }

        var asset = UpdateChecker.FindReleaseAsset(update);
        if (asset == null)
        {
            SendProgress("Update error", "No compatible release asset found");
            await Task.Delay(1000);
            return;
        }

        await DownloadAndApplyUpdateAsync(update, asset);
    }

    private async Task DownloadAndApplyUpdateAsync(GitHubRelease update, GitHubAsset asset)
    {
        SendProgress("Downloading update", $"Version {update.GetVersion()}");

        using var downloader = new UpdateDownloader();

        downloader.OnProgress += (downloaded, total) =>
        {
            var progress = total > 0 ? (double)downloaded / total : 0;
            SendProgress("Downloading update", $"{downloaded / 1024 / 1024:F1} MB / {total / 1024 / 1024:F1} MB", progress);
        };

        downloader.OnStatus += status =>
        {
            SendProgress("Updating", status);
        };

        var success = await downloader.DownloadAndApplyAsync(asset);

        if (success)
        {
            SendProgress("Update complete", $"Updated to version {update.GetVersion()}");
            await Task.Delay(500);
        }
        else
        {
            SendProgress("Update failed", "Continuing with current version");
            await Task.Delay(1000);
        }
    }

    private async Task CheckAndUpdateModsAsync()
    {
        var modLoaderRoot = EnvVars.GetModLoaderRoot();

        using var modManager = new ModManager(modLoaderRoot);

        modManager.OnProgress += (stage, description, progress) =>
        {
            SendProgress(stage, description, progress);
        };

        try
        {
            var updatedCount = await modManager.UpdateModsAsync();

            if (updatedCount > 0)
            {
                SendProgress("Mods updated", $"{updatedCount} mod(s) updated");
                await Task.Delay(300);
            }
        }
        catch
        {
            // Continue even if mod updates fail
        }
    }

    private void LoadAndRunLoader()
    {
        SendProgress("Loading", "Starting mod loader...");

        var loaderPath = Path.Combine(_coreDirectory, LoaderAssemblyName);

        if (!File.Exists(loaderPath))
        {
            throw new FileNotFoundException($"Loader assembly not found: {loaderPath}");
        }

        var loaderAssembly = Assembly.LoadFrom(loaderPath);

        // Initialize EnoUnityLoader's EnvVars before calling PreloaderMain
        InitializeLoaderEnvVars(loaderAssembly);

        var entrypointType = loaderAssembly.GetType($"{LoaderEntrypointNamespace}.{LoaderEntrypointClass}")
            ?? throw new TypeLoadException($"Entry point type not found: {LoaderEntrypointNamespace}.{LoaderEntrypointClass}");

        var entrypointMethod = entrypointType.GetMethod(LoaderEntrypointMethod, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException($"Entry point method not found: {LoaderEntrypointMethod}");

        try
        {
            entrypointMethod.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap the TargetInvocationException to show the real error
            throw ex.InnerException;
        }
    }

    private static void InitializeLoaderEnvVars(Assembly loaderAssembly)
    {
        // EnoUnityLoader has its own EnvVars class that needs to be initialized
        var envVarsType = loaderAssembly.GetType("EnoUnityLoader.Preloader.EnvVars");
        if (envVarsType == null)
            return;

        var loadVarsMethod = envVarsType.GetMethod("LoadVars", BindingFlags.Public | BindingFlags.Static);
        loadVarsMethod?.Invoke(null, null);
    }

    private void SendProgress(string stage, string description, double progress = -1)
    {
        if (_ipcClient == null || !_ipcClient.IsConnected) return;

        _ = SendProgressSafeAsync(stage, description, progress);
    }

    private async Task SendProgressSafeAsync(string stage, string description, double progress)
    {
        try
        {
            if (_ipcClient != null && _ipcClient.IsConnected)
            {
                await _ipcClient.SendProgressAsync(stage, description, progress);
            }
        }
        catch
        {
            // Ignore pipe errors - UI may have closed
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectUiAsync();
    }
}
