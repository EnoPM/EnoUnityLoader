using System.Net.Http.Json;
using System.Reflection;
using EnoUnityLoader.Updater.GitHub;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnoUnityLoader.Updater.Mods;

/// <summary>
/// Manages mod installation and updates from GitHub releases.
/// </summary>
internal sealed class ModManager : IDisposable
{
    private const string ConfigFileName = "mods.yaml";
    private const string GitHubApiBase = "https://api.github.com";

    private readonly HttpClient _httpClient;
    private readonly string _modsDirectory;
    private readonly string _configPath;

    public event Action<string, string, double>? OnProgress;

    public ModManager(string modLoaderRoot)
    {
        _modsDirectory = Path.Combine(modLoaderRoot, "mods");
        _configPath = Path.Combine(modLoaderRoot, ConfigFileName);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EnoUnityLoader-ModManager");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    /// <summary>
    /// Checks and updates all enabled mods from the config file.
    /// </summary>
    public async Task<int> UpdateModsAsync(CancellationToken cancellationToken = default)
    {
        var config = LoadConfig();
        if (config == null || config.Mods.Count == 0)
            return 0;

        var enabledMods = config.Mods.Where(m => m.Enabled).ToList();
        if (enabledMods.Count == 0)
            return 0;

        var updatedCount = 0;

        for (var i = 0; i < enabledMods.Count; i++)
        {
            var mod = enabledMods[i];
            var progress = (double)i / enabledMods.Count;

            try
            {
                var updated = await UpdateModAsync(mod, progress, cancellationToken);
                if (updated)
                    updatedCount++;
            }
            catch
            {
                // Continue with next mod on error
            }
        }

        return updatedCount;
    }

    private async Task<bool> UpdateModAsync(ModEntry mod, double baseProgress, CancellationToken cancellationToken)
    {
        OnProgress?.Invoke($"Checking {mod.Name}", "Fetching release info...", baseProgress);

        // Get latest release from GitHub
        var release = await GetLatestReleaseAsync(mod.Repo, cancellationToken);
        if (release == null)
            return false;

        var releaseVersion = release.GetVersion();
        if (releaseVersion == null)
            return false;

        // Check installed version
        var installedVersion = GetInstalledVersion(mod);

        // Compare versions
        if (installedVersion != null && installedVersion >= releaseVersion)
        {
            OnProgress?.Invoke($"{mod.Name}", $"Up to date (v{installedVersion})", baseProgress);
            return false;
        }

        // Find DLL assets to download
        var dllAssets = release.Assets
            .Where(a => a.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dllAssets.Count == 0)
            return false;

        // Download and install
        OnProgress?.Invoke($"Updating {mod.Name}", $"v{installedVersion} → v{releaseVersion}", baseProgress);

        var modDirectory = Path.Combine(_modsDirectory, mod.Name);
        Directory.CreateDirectory(modDirectory);

        foreach (var asset in dllAssets)
        {
            var destPath = Path.Combine(modDirectory, asset.Name);
            await DownloadFileAsync(asset.BrowserDownloadUrl, destPath, cancellationToken);
        }

        OnProgress?.Invoke($"{mod.Name}", $"Updated to v{releaseVersion}", baseProgress);
        return true;
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(string repo, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{GitHubApiBase}/repos/{repo}/releases/latest";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private Version? GetInstalledVersion(ModEntry mod)
    {
        var assemblyPath = Path.Combine(_modsDirectory, mod.Name, mod.MainAssembly);

        if (!File.Exists(assemblyPath))
            return null;

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            return assemblyName.Version;
        }
        catch
        {
            return null;
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, cancellationToken);
    }

    private ModsConfig? LoadConfig()
    {
        if (!File.Exists(_configPath))
            return null;

        try
        {
            var yaml = File.ReadAllText(_configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return deserializer.Deserialize<ModsConfig>(yaml);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
