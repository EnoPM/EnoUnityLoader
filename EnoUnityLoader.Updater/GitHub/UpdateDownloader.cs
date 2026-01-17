using System.IO.Compression;

namespace EnoUnityLoader.Updater.GitHub;

internal sealed class UpdateDownloader : IDisposable
{
    private readonly HttpClient _httpClient;

    public event Action<long, long>? OnProgress;
    public event Action<string>? OnStatus;

    public UpdateDownloader()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EnoUnityLoader-Updater");
    }

    /// <summary>
    /// Downloads and applies an update from the given asset.
    /// </summary>
    public async Task<bool> DownloadAndApplyAsync(GitHubAsset asset, CancellationToken cancellationToken = default)
    {
        var modLoaderRoot = EnvVars.GetModLoaderRoot();
        var tempDir = Path.Combine(modLoaderRoot, "update-temp");
        var tempZipPath = Path.Combine(tempDir, "update.zip");

        try
        {
            // Create temp directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            // Download the zip file
            OnStatus?.Invoke("Downloading update...");
            await DownloadFileAsync(asset.BrowserDownloadUrl, tempZipPath, asset.Size, cancellationToken);

            // Extract the zip file
            OnStatus?.Invoke("Extracting update...");
            await ExtractUpdateAsync(tempZipPath, modLoaderRoot, cancellationToken);

            OnStatus?.Invoke("Update complete!");
            return true;
        }
        catch (Exception ex)
        {
            OnStatus?.Invoke($"Update failed: {ex.Message}");
            return false;
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task DownloadFileAsync(string url, string destinationPath, long expectedSize, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        var buffer = new byte[81920];
        long bytesRead = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

        int read;
        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;
            OnProgress?.Invoke(bytesRead, totalBytes);
        }
    }

    private static Task ExtractUpdateAsync(string zipPath, string destinationRoot, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip files that are currently in use
                if (IsLockedFile(entry.FullName))
                    continue;

                // Skip directories
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                // Determine the destination path
                // The zip structure is: EnoUnityLoader/core/... -> extract to modLoaderRoot/core/...
                var relativePath = entry.FullName;

                // Remove the leading "EnoUnityLoader/" if present
                if (relativePath.StartsWith("EnoUnityLoader/", StringComparison.OrdinalIgnoreCase))
                    relativePath = relativePath["EnoUnityLoader/".Length..];

                var destinationPath = Path.Combine(destinationRoot, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                // Extract the file, overwriting if exists
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Files that cannot be updated because they are in use by the updater or UI process.
    /// </summary>
    private static readonly string[] LockedFiles =
    [
        "EnoUnityLoader.Updater.dll",  // Currently executing
        "EnoUnityLoader.Ipc.dll",       // Loaded by updater
        "EnoUnityLoader.Ui.exe"         // Running as separate process
    ];

    private static bool IsLockedFile(string entryPath)
    {
        foreach (var lockedFile in LockedFiles)
        {
            if (entryPath.Contains(lockedFile, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
