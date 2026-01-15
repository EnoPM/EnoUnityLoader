using System.IO;
using System.Linq;
using System.Reflection;
using SemanticVersioning;

namespace EnoUnityLoader;

/// <summary>
/// Paths used by ModLoader
/// </summary>
public static class Paths
{
    private const string LoaderDirectoryName = "EnoUnityLoader";
    
    /// <summary>
    /// ModLoader version.
    /// </summary>
    public static Version ModLoaderVersion { get; } =
        Version.Parse(typeof(Paths).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                                    .InformationalVersion);

    /// <summary>
    /// The path to the Managed folder that contains the main managed assemblies.
    /// </summary>
    public static string ManagedPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the game data folder of the currently running Unity game.
    /// </summary>
    public static string GameDataPath { get; private set; } = string.Empty;

    /// <summary>
    /// The directory that the core ModLoader DLLs reside in.
    /// </summary>
    public static string ModLoaderAssemblyDirectory { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the core ModLoader DLL.
    /// </summary>
    public static string ModLoaderAssemblyPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the main ModLoader folder.
    /// </summary>
    public static string ModLoaderRootPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path of the currently executing program ModLoader is encapsulated in.
    /// </summary>
    public static string ExecutablePath { get; private set; } = string.Empty;

    /// <summary>
    /// The directory that the currently executing process resides in.
    /// <para>On OSX however, this is the parent directory of the game.app folder.</para>
    /// </summary>
    public static string GameRootPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the config directory.
    /// </summary>
    public static string ConfigPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the global ModLoader configuration file.
    /// </summary>
    public static string ModLoaderConfigPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to temporary cache files.
    /// </summary>
    public static string CachePath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the patcher plugin folder which resides in the ModLoader folder.
    /// </summary>
    public static string PatcherPluginPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the plugin patcher folder which resides in the patchers folder.
    /// Plugin patchers can modify plugin assemblies before they are loaded.
    /// </summary>
    public static string PluginPatcherPath { get; private set; } = string.Empty;

    /// <summary>
    /// The path to the plugin folder which resides in the ModLoader folder.
    /// <para>
    /// This is ONLY guaranteed to be set correctly when Chainloader has been initialized.
    /// </para>
    /// </summary>
    public static string PluginPath { get; private set; } = string.Empty;

    /// <summary>
    /// The name of the currently executing process.
    /// </summary>
    public static string ProcessName { get; private set; } = string.Empty;

    /// <summary>
    /// List of directories from where assemblies will be searched before assembly resolving is invoked.
    /// </summary>
    public static string[] DllSearchPaths { get; private set; } = [];

    /// <summary>
    /// Initializes all paths based on the executable path.
    /// </summary>
    /// <param name="executablePath">Path to the game executable.</param>
    /// <param name="bepinRootPath">Optional custom ModLoader root path.</param>
    /// <param name="managedPath">Optional custom managed assemblies path.</param>
    /// <param name="gameDataRelativeToManaged">If true, GameDataPath is derived from managedPath's parent.</param>
    /// <param name="dllSearchPath">Optional additional DLL search paths.</param>
    public static void SetExecutablePath(string executablePath,
                                         string? bepinRootPath = null,
                                         string? managedPath = null,
                                         bool gameDataRelativeToManaged = false,
                                         string[]? dllSearchPath = null)
    {
        ExecutablePath = executablePath;
        ProcessName = Path.GetFileNameWithoutExtension(executablePath);

        GameRootPath = PlatformHelper.Is(Platform.MacOS)
                           ? Utility.ParentDirectory(executablePath, 4)
                           : Path.GetDirectoryName(executablePath) ?? string.Empty;

        if (managedPath != null && gameDataRelativeToManaged)
        {
            GameDataPath = Path.GetDirectoryName(managedPath) ?? string.Empty;
        }
        else
        {
            // According to some experiments, Unity checks whether globalgamemanagers/data.unity3d exists in the data folder before picking it.
            // 'ProcessName_Data' folder is checked first, then if that fails 'Data' folder is checked. If neither is valid, the player crashes.
            // A simple Directory.Exists check is accurate enough while being less likely to break in case these conditions change.
            GameDataPath = Path.Combine(GameRootPath, $"{ProcessName}_Data");
            if (!Directory.Exists(GameDataPath))
                GameDataPath = Path.Combine(GameRootPath, "Data");
        }

        if (string.IsNullOrEmpty(GameDataPath) || !Directory.Exists(GameDataPath))
            throw new DirectoryNotFoundException("Failed to extract valid GameDataPath from executablePath: " + executablePath);

        ManagedPath = managedPath ?? Path.Combine(GameDataPath, "Managed");
        ModLoaderRootPath = bepinRootPath ?? Path.Combine(GameRootPath, LoaderDirectoryName);
        ConfigPath = Path.Combine(ModLoaderRootPath, "config");
        ModLoaderConfigPath = Path.Combine(ConfigPath, $"{LoaderDirectoryName}.cfg");
        PluginPath = Path.Combine(ModLoaderRootPath, "mods");
        PatcherPluginPath = Path.Combine(ModLoaderRootPath, "patchers");
        PluginPatcherPath = Path.Combine(PatcherPluginPath, "plugins");
        ModLoaderAssemblyDirectory = Path.Combine(ModLoaderRootPath, "core");
        ModLoaderAssemblyPath = Path.Combine(ModLoaderAssemblyDirectory,
                                           $"{Assembly.GetExecutingAssembly().GetName().Name}.dll");
        CachePath = Path.Combine(ModLoaderRootPath, "cache");
        DllSearchPaths = (dllSearchPath ?? []).Concat([ManagedPath]).Distinct().ToArray();
    }

    internal static void SetPluginPath(string pluginPath) =>
        PluginPath = Utility.CombinePaths(ModLoaderRootPath, pluginPath);
}
