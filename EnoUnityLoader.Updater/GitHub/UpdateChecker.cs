using System.Net.Http.Json;
using System.Reflection;

namespace EnoUnityLoader.Updater.GitHub;

internal sealed class UpdateChecker : IDisposable
{
    private const string GitHubApiBase = "https://api.github.com";
    private const string Owner = "EnoPM";
    private const string Repo = "EnoUnityLoader";

    private readonly HttpClient _httpClient;

    public UpdateChecker()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"EnoUnityLoader-Updater/{GetCurrentVersion()}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    /// <summary>
    /// Gets the current installed version of EnoUnityLoader from the assembly.
    /// </summary>
    public Version GetCurrentVersion()
    {
        var loaderPath = Path.Combine(EnvVars.GetCoreDirectory(), "EnoUnityLoader.dll");

        if (!File.Exists(loaderPath))
            return new Version(0, 0, 0);

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(loaderPath);
            return assemblyName.Version ?? new Version(0, 0, 0);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }

    /// <summary>
    /// Checks for updates and returns the latest release if newer than current version.
    /// </summary>
    public async Task<GitHubRelease?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestRelease = await GetLatestReleaseAsync(cancellationToken);
            if (latestRelease == null)
                return null;

            var latestVersion = latestRelease.GetVersion();
            if (latestVersion == null)
                return null;

            var currentVersion = GetCurrentVersion();
            return latestVersion > currentVersion ? latestRelease : null;
        }
        catch
        {
            // Network errors, parsing errors, etc. - just return null
            return null;
        }
    }

    /// <summary>
    /// Gets the latest release from GitHub.
    /// </summary>
    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{GitHubApiBase}/repos/{Owner}/{Repo}/releases/latest";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);
    }

    /// <summary>
    /// Finds the main release asset (the zip file).
    /// </summary>
    public static GitHubAsset? FindReleaseAsset(GitHubRelease release)
    {
        // Look for the main zip file (e.g., "EnoUnityLoader-v1.0.0.zip")
        return release.Assets.FirstOrDefault(a =>
            a.Name.StartsWith("EnoUnityLoader", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
