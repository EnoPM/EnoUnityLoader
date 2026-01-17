namespace EnoUnityLoader.Updater;

/// <summary>
/// Doorstop environment variables.
/// </summary>
internal static class EnvVars
{
    /// <summary>
    /// Path to the assembly that was invoked via Doorstop.
    /// </summary>
    public static string? DoorstopInvokeDllPath { get; private set; }

    /// <summary>
    /// Full path to the game's "Managed" folder.
    /// </summary>
    public static string? DoorstopManagedFolderDir { get; private set; }

    /// <summary>
    /// Full path to the game executable currently running.
    /// </summary>
    public static string? DoorstopProcessPath { get; private set; }

    /// <summary>
    /// Array of paths where Mono searches DLLs from.
    /// </summary>
    public static string[] DoorstopDllSearchDirs { get; private set; } = [];

    public static void Load()
    {
        DoorstopInvokeDllPath = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
        DoorstopManagedFolderDir = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
        DoorstopProcessPath = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
        DoorstopDllSearchDirs =
            Environment.GetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS")?.Split(Path.PathSeparator) ?? [];
    }

    /// <summary>
    /// Gets the ModLoader root directory (parent of core folder).
    /// </summary>
    public static string GetModLoaderRoot()
    {
        if (string.IsNullOrEmpty(DoorstopInvokeDllPath))
            throw new InvalidOperationException("DOORSTOP_INVOKE_DLL_PATH is not set");

        var coreDir = Path.GetDirectoryName(DoorstopInvokeDllPath)!;
        return Path.GetDirectoryName(coreDir)!;
    }

    /// <summary>
    /// Gets the core directory where loader DLLs are located.
    /// </summary>
    public static string GetCoreDirectory()
    {
        if (string.IsNullOrEmpty(DoorstopInvokeDllPath))
            throw new InvalidOperationException("DOORSTOP_INVOKE_DLL_PATH is not set");

        return Path.GetDirectoryName(DoorstopInvokeDllPath)!;
    }
}
