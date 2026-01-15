using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EnoUnityLoader.AssemblyLoading;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Configuration;
using EnoUnityLoader.Console;
using EnoUnityLoader.Contract;
using EnoUnityLoader.Logging;
using EnoUnityLoader.PluginPatching;
using Mono.Cecil;

namespace EnoUnityLoader.Bootstrap;

/// <summary>
///     Base chain loader for loading ModLoader plugins.
/// </summary>
/// <typeparam name="TPlugin">The base plugin type.</typeparam>
public abstract partial class BaseChainLoader<TPlugin>
{
    /// <summary>
    ///     Name of the currently executing assembly.
    /// </summary>
    protected static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "EnoUnityLoader";

    /// <summary>
    ///     Version of the currently executing assembly.
    /// </summary>
    protected static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);

    private static Regex AllowedGuidRegex { get; } = GetAllowedGuidRegex();

    /// <summary>
    ///     Analyzes the given type definition and attempts to convert it to a valid <see cref="PluginInfo" />
    /// </summary>
    /// <param name="type">Type definition to analyze.</param>
    /// <param name="assemblyLocation">The filepath of the assembly, to keep as metadata.</param>
    /// <returns>If the type represent a valid plugin, returns a <see cref="PluginInfo" /> instance. Otherwise, return null.</returns>
    public static PluginInfo? ToPluginInfo(TypeDefinition type, string assemblyLocation)
    {
        if (type.IsInterface || type.IsAbstract)
            return null;

        try
        {
            if (!type.IsSubtypeOf(typeof(TPlugin)))
                return null;
        }
        catch (AssemblyResolutionException)
        {
            // Can happen if this type inherits a type from an assembly that can't be found. Safe to assume it's not a plugin.
            return null;
        }

        var metadata = ModInfosAttribute.FromCecilType(type);

        // Perform checks that will prevent the plugin from being loaded in ALL cases
        if (metadata == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping over type [{type.FullName}] as no metadata attribute is specified");
            return null;
        }

        if (string.IsNullOrEmpty(metadata.Guid) || !AllowedGuidRegex.IsMatch(metadata.Guid))
        {
            Logger.Log(LogLevel.Warning,
                       $"Skipping type [{type.FullName}] because its GUID [{metadata.Guid}] is of an illegal format.");
            return null;
        }

        if (metadata.Version == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its version is invalid.");
            return null;
        }

        if (metadata.Name == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its name is null.");
            return null;
        }

        var filters = ModProcessAttribute.FromCecilType(type);
        var dependencies = ModDependencyAttribute.FromCecilType(type);
        var incompatibilities = ModIncompatibilityAttribute.FromCecilType(type);

        var bepinVersion =
            type.Module.AssemblyReferences.FirstOrDefault(reference => reference.Name == CurrentAssemblyName)?.Version ??
            new Version();

        return new PluginInfo
        {
            Metadata = metadata,
            Processes = filters,
            Dependencies = dependencies,
            Incompatibilities = incompatibilities,
            TypeName = type.FullName,
            TargettedModLoaderVersion = bepinVersion,
            Location = assemblyLocation
        };
    }

    /// <summary>
    ///     Checks if the assembly has ModLoader plugins.
    /// </summary>
    protected static bool HasModLoaderPlugins(AssemblyDefinition ass)
    {
        if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName))
            return false;
        if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(ModInfosAttribute).FullName))
            return false;

        return true;
    }

    /// <summary>
    ///     Checks if the plugin targets the wrong version of ModLoader.
    /// </summary>
    protected static bool PluginTargetsWrongModLoader(PluginInfo pluginInfo)
    {
        var pluginTarget = pluginInfo.TargettedModLoaderVersion;
        // X.X.X.x - compare normally. x.x.x.X - nightly build number, ignore
        if (pluginTarget.Major != CurrentAssemblyVersion.Major) return true;
        if (pluginTarget.Minor > CurrentAssemblyVersion.Minor) return true;
        if (pluginTarget.Minor < CurrentAssemblyVersion.Minor) return false;
        return pluginTarget.Build > CurrentAssemblyVersion.Build;
    }

    #region Contract

    /// <summary>
    ///     Title for the console window.
    /// </summary>
    protected virtual string ConsoleTitle => $"EnoUnityLoader {Paths.ModLoaderVersion} - {Paths.ProcessName}";

    private bool _initialized;

    /// <summary>
    /// Engine for patching plugin assemblies before they are loaded.
    /// </summary>
    private PluginPatcherEngine? _pluginPatcherEngine;

    /// <summary>
    ///     List of all <see cref="PluginInfo" /> instances loaded via the chainloader.
    /// </summary>
    public Dictionary<string, PluginInfo> Plugins { get; } = new();

    /// <summary>
    ///     Collection of error chainloader messages that occured during plugin loading.
    ///     Contains information about what certain plugins were not loaded.
    /// </summary>
    public List<string> DependencyErrors { get; } = [];

    /// <summary>
    ///     Occurs after a plugin is loaded.
    /// </summary>
    public event Action<PluginInfo>? PluginLoaded;

    /// <summary>
    ///     Occurs after all plugins are loaded.
    /// </summary>
    public event Action? Finished;

    /// <summary>
    ///     Initializes the chainloader.
    /// </summary>
    /// <param name="gameExePath">Optional path to the game executable.</param>
    public virtual void Initialize(string? gameExePath = null)
    {
        if (_initialized)
            throw new InvalidOperationException("Chainloader cannot be initialized multiple times");

        // Set vitals
        if (gameExePath != null)
            // Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
            // This is used by Preloader to use environment variables, for example
            Paths.SetExecutablePath(gameExePath);

        InitializeLoggers();

        if (!Directory.Exists(Paths.PluginPath))
            Directory.CreateDirectory(Paths.PluginPath);

        if (!Directory.Exists(Paths.PatcherPluginPath))
            Directory.CreateDirectory(Paths.PatcherPluginPath);

        if (!Directory.Exists(Paths.PluginPatcherPath))
            Directory.CreateDirectory(Paths.PluginPatcherPath);

        // Initialize plugin patcher engine
        if (PluginPatcherEngine.IsEnabled)
        {
            _pluginPatcherEngine = new PluginPatcherEngine();
            _pluginPatcherEngine.LoadPatchersFromDirectory(Paths.PluginPatcherPath);
        }

        _initialized = true;

        Logger.Log(LogLevel.Message, "Chainloader initialized");
    }

    /// <summary>
    ///     Initializes the logging system.
    /// </summary>
    protected virtual void InitializeLoggers()
    {
        if (ConsoleManager.ConsoleEnabled && !ConsoleManager.ConsoleActive)
            ConsoleManager.CreateConsole();

        if (ConsoleManager.ConsoleActive)
        {
            if (!Logger.Listeners.Any(x => x is ConsoleLogListener))
                Logger.Listeners.Add(new ConsoleLogListener());

            ConsoleManager.SetConsoleTitle(ConsoleTitle);
        }

        if (ConfigDiskLogging.Value)
            Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskLoggingDisplayedLevel.Value,
                                                     ConfigDiskAppend.Value, ConfigDiskLoggingInstantFlushing.Value,
                                                     ConfigDiskLoggingFileLimit.Value));

        if (!TraceLogSource.IsListening)
            Logger.Sources.Add(TraceLogSource.CreateSource());

        if (!Logger.Sources.Any(x => x is HarmonyLogSource))
            Logger.Sources.Add(new HarmonyLogSource());
    }

    /// <summary>
    /// Discovers all plugins in the plugin directory without loading them.
    /// </summary>
    /// <remarks>
    /// This is useful for discovering ModLoader plugin metadata.
    /// </remarks>
    /// <param name="path">Path from which to search the plugins.</param>
    /// <param name="cacheName">Cache name to use. If null, results are not cached.</param>
    /// <returns>List of discovered plugins and their metadata.</returns>
    protected IList<PluginInfo> DiscoverPluginsFrom(string path, string cacheName = "chainloader")
    {
        var pluginsToLoad =
            TypeLoader.FindPluginTypes(path, ToPluginInfo, HasModLoaderPlugins, cacheName);
        return pluginsToLoad.SelectMany(p => p.Value).ToList();
    }

    /// <summary>
    /// Discovers plugins to load.
    /// </summary>
    /// <returns>List of plugins to be loaded.</returns>
    protected virtual IList<PluginInfo> DiscoverPlugins()
    {
        return DiscoverPluginsFrom(Paths.PluginPath);
    }

    /// <summary>
    /// Preprocess the plugins and modify the load order.
    /// </summary>
    /// <remarks>Some plugins may be skipped if they cannot be loaded (wrong metadata, etc).</remarks>
    /// <param name="plugins">Plugins to process.</param>
    /// <returns>List of plugins to load in the correct load order.</returns>
    protected virtual IList<PluginInfo> ModifyLoadOrder(IList<PluginInfo> plugins)
    {
        // We use a sorted dictionary to ensure consistent load order
        var dependencyDict =
            new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
        var pluginsByGuid = new Dictionary<string, PluginInfo>();

        foreach (var pluginInfoGroup in plugins.GroupBy(info => info.Metadata?.Guid ?? string.Empty))
        {
            if (string.IsNullOrEmpty(pluginInfoGroup.Key))
                continue;

            if (Plugins.TryGetValue(pluginInfoGroup.Key, out var loadedPlugin))
            {
                Logger.Log(LogLevel.Warning,
                           $"Skipping [{pluginInfoGroup.Key}] because a plugin with a similar GUID ([{loadedPlugin}]) has been already loaded.");
                continue;
            }

            PluginInfo? loadedVersion = null;
            foreach (var pluginInfo in pluginInfoGroup.OrderByDescending(x => x.Metadata?.Version))
            {
                if (loadedVersion != null)
                {
                    Logger.Log(LogLevel.Warning,
                               $"Skipping [{pluginInfo}] because a newer version exists ({loadedVersion})");
                    continue;
                }

                // Perform checks that will prevent loading plugins in this run
                var filters = pluginInfo.Processes.ToList();
                var invalidProcessName = filters.Count != 0 &&
                                         filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""),
                                                                         Paths.ProcessName,
                                                                         StringComparison
                                                                             .InvariantCultureIgnoreCase));

                if (invalidProcessName)
                {
                    Logger.Log(LogLevel.Warning,
                               $"Skipping [{pluginInfo}] because of process filters ({string.Join(", ", pluginInfo.Processes.Select(p => p.ProcessName).ToArray())})");
                    continue;
                }

                loadedVersion = pluginInfo;
                dependencyDict[pluginInfo.Metadata!.Guid] = pluginInfo.Dependencies.Select(d => d.DependencyGuid);
                pluginsByGuid[pluginInfo.Metadata.Guid] = pluginInfo;
            }
        }

        foreach (var pluginInfo in pluginsByGuid.Values.ToList())
            if (pluginInfo.Incompatibilities.Any(incompatibility =>
                                                     pluginsByGuid.ContainsKey(incompatibility.IncompatibleGuid)
                                                  || Plugins.ContainsKey(incompatibility.IncompatibleGuid))
               )
            {
                pluginsByGuid.Remove(pluginInfo.Metadata!.Guid);
                dependencyDict.Remove(pluginInfo.Metadata.Guid);

                var incompatiblePluginsNew = pluginInfo.Incompatibilities.Select(x => x.IncompatibleGuid)
                                                       .Where(x => pluginsByGuid.ContainsKey(x));
                var incompatiblePluginsExisting = pluginInfo.Incompatibilities.Select(x => x.IncompatibleGuid)
                                                            .Where(x => Plugins.ContainsKey(x));
                var incompatiblePlugins = incompatiblePluginsNew.Concat(incompatiblePluginsExisting).ToArray();
                var message =
                    $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Error, message);
            }
            else if (PluginTargetsWrongModLoader(pluginInfo))
            {
                var message =
                    $@"Plugin [{pluginInfo}] targets a wrong version of ModLoader ({pluginInfo.TargettedModLoaderVersion}) and might not work until you update";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Warning, message);
            }

        // We don't add already loaded plugins to the dependency graph as they are already loaded

        var emptyDependencies = Array.Empty<string>();

        // Sort plugins by their dependencies.
        // Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
        var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys,
                                                    x =>
                                                        dependencyDict.TryGetValue(x, out var deps)
                                                            ? deps
                                                            : emptyDependencies).ToList();

        return sortedPlugins.Where(pluginsByGuid.ContainsKey).Select(x => pluginsByGuid[x]).ToList();
    }

    /// <summary>
    /// Run the chainloader and load all plugins from the plugins folder.
    /// </summary>
    public virtual void Execute()
    {
        try
        {
            var plugins = DiscoverPlugins();
            Logger.Log(LogLevel.Info, $"{plugins.Count} plugin{(plugins.Count == 1 ? "" : "s")} to load");
            LoadPlugins(plugins);
            Finished?.Invoke();
        }
        catch (Exception ex)
        {
            try
            {
                ConsoleManager.CreateConsole();
            }
            catch { }

            Logger.Log(LogLevel.Error, $"Error occurred loading plugins: {ex}");
        }

        Logger.Log(LogLevel.Message, "Chainloader startup complete");
    }

    private IList<PluginInfo> LoadPlugins(IList<PluginInfo> plugins)
    {
        var sortedPlugins = ModifyLoadOrder(plugins);

        var invalidPlugins = new HashSet<string>();
        var processedPlugins = new Dictionary<string, SemanticVersioning.Version>();
        var loadedAssemblies = new Dictionary<string, Assembly>();
        var loadedPlugins = new List<PluginInfo>();

        foreach (var plugin in sortedPlugins)
        {
            var dependsOnInvalidPlugin = false;
            var missingDependencies = new List<ModDependencyAttribute>();
            foreach (var dependency in plugin.Dependencies)
            {
                static bool IsHardDependency(ModDependencyAttribute dep) =>
                    (dep.Flags & ModDependencyAttribute.DependencyFlags.HardDependency) != 0;

                // If the dependency wasn't already processed, it's missing altogether
                var dependencyExists =
                    processedPlugins.TryGetValue(dependency.DependencyGuid, out var pluginVersion);
                // Alternatively, if the dependency hasn't been loaded before, it's missing too
                if (!dependencyExists)
                {
                    dependencyExists = Plugins.TryGetValue(dependency.DependencyGuid, out var pluginInfo);
                    pluginVersion = pluginInfo?.Metadata?.Version;
                }

                if (!dependencyExists || dependency.VersionRange != null &&
                    !dependency.VersionRange.IsSatisfied(pluginVersion))
                {
                    // If the dependency is hard, collect it into a list to show
                    if (IsHardDependency(dependency))
                        missingDependencies.Add(dependency);
                    continue;
                }

                // If the dependency is a hard and is invalid (e.g. has missing dependencies), report that to the user
                if (invalidPlugins.Contains(dependency.DependencyGuid) && IsHardDependency(dependency))
                {
                    dependsOnInvalidPlugin = true;
                    break;
                }
            }

            processedPlugins.Add(plugin.Metadata!.Guid, plugin.Metadata.Version!);

            if (dependsOnInvalidPlugin)
            {
                var message =
                    $"Skipping [{plugin}] because it has a dependency that was not loaded. See previous errors for details.";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Warning, message);
                continue;
            }

            if (missingDependencies.Count != 0)
            {
                var message = $@"Could not load [{plugin}] because it has missing dependencies: {
                    string.Join(", ", missingDependencies.Select(s => s.VersionRange == null ? s.DependencyGuid : $"{s.DependencyGuid} ({s.VersionRange})").ToArray())
                }";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Error, message);

                invalidPlugins.Add(plugin.Metadata.Guid);
                continue;
            }

            try
            {
                Logger.Log(LogLevel.Info, $"Loading [{plugin}]");

                if (!loadedAssemblies.TryGetValue(plugin.Location, out var ass))
                {
                    // Check if assembly was already patched and loaded by preloader
                    var fileName = Path.GetFileName(plugin.Location);
                    if (!TryGetPatchedAssembly(fileName, out ass))
                    {
                        // Check for plugin patcher cached or patched assembly
                        if (!TryGetPluginPatchedAssembly(plugin, out ass))
                        {
                            // Use AssemblyLoadContext instead of Assembly.LoadFrom for .NET 10 compatibility
                            ass = ModLoaderAssemblyLoadContext.Default.LoadFromPath(plugin.Location);
                        }
                    }
                    loadedAssemblies[plugin.Location] = ass;
                }

                Plugins[plugin.Metadata.Guid] = plugin;
                TryRunModuleCtor(plugin, ass);
                plugin.Instance = LoadPlugin(plugin, ass);
                loadedPlugins.Add(plugin);

                PluginLoaded?.Invoke(plugin);
            }
            catch (Exception ex)
            {
                invalidPlugins.Add(plugin.Metadata.Guid);
                Plugins.Remove(plugin.Metadata.Guid);

                Logger.Log(LogLevel.Error,
                           $"Error loading [{plugin}]: {(ex is ReflectionTypeLoadException re ? TypeLoader.TypeLoadExceptionToString(re) : ex.ToString())}");
            }
        }

        return loadedPlugins;
    }

    /// <summary>
    /// Detects and loads all plugins in the specified directories.
    /// </summary>
    /// <remarks>
    /// It is better to collect all paths at once and use a single call to LoadPlugins than multiple calls.
    /// This allows to run proper dependency resolving and to load all plugins in one go.
    /// </remarks>
    /// <param name="pluginsPaths">Directories to search the plugins from.</param>
    /// <returns>List of loaded plugin infos.</returns>
    public IList<PluginInfo> LoadPlugins(params string[] pluginsPaths)
    {
        // TODO: This is a temporary solution for 3rd party loaders. Instead, this should be done via metaplugins.
        var plugins = new List<PluginInfo>();
        foreach (var pluginsPath in pluginsPaths)
            plugins.AddRange(DiscoverPluginsFrom(pluginsPath));
        return LoadPlugins(plugins);
    }

    private static void TryRunModuleCtor(PluginInfo plugin, Assembly assembly)
    {
        try
        {
            var type = assembly.GetType(plugin.TypeName);
            if (type != null)
                RuntimeHelpers.RunModuleConstructor(type.Module.ModuleHandle);
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warning,
                       $"Couldn't run Module constructor for {assembly.FullName}::{plugin.TypeName}: {e}");
        }
    }

    /// <summary>
    ///     Tries to get a patched assembly from the preloader.
    /// </summary>
    /// <param name="fileName">The filename of the assembly.</param>
    /// <param name="assembly">The patched assembly if found.</param>
    /// <returns>True if the patched assembly was found.</returns>
    protected virtual bool TryGetPatchedAssembly(string fileName, [NotNullWhen(true)] out Assembly? assembly)
    {
        assembly = null;
        return false;
    }

    /// <summary>
    ///     Tries to get a plugin-patched assembly from the plugin patcher engine.
    ///     This checks for cached patched assemblies or applies patching if needed.
    /// </summary>
    /// <param name="pluginInfo">The plugin info.</param>
    /// <param name="assembly">The patched assembly if found or patched.</param>
    /// <returns>True if a patched assembly was obtained.</returns>
    protected virtual bool TryGetPluginPatchedAssembly(PluginInfo pluginInfo, [NotNullWhen(true)] out Assembly? assembly)
    {
        assembly = null;

        if (_pluginPatcherEngine == null || _pluginPatcherEngine.Context.Patchers.Count == 0)
            return false;

        // First check if there's a cached patched assembly
        if (_pluginPatcherEngine.TryGetCachedAssembly(pluginInfo, out assembly) && assembly != null)
            return true;

        // Try to patch the plugin (this will check if any patchers target it)
        assembly = _pluginPatcherEngine.PatchPlugin(pluginInfo);
        return assembly != null;
    }

    /// <summary>
    ///     Loads a plugin from the given assembly.
    /// </summary>
    /// <param name="pluginInfo">Plugin metadata.</param>
    /// <param name="pluginAssembly">Assembly containing the plugin.</param>
    /// <returns>The loaded plugin instance.</returns>
    public abstract TPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly);

    #endregion

    #region Config

    private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "AppendLog",
     false,
     "Appends to the log file instead of overwriting, on game startup.");

    private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "Enabled",
     true,
     "Enables writing log messages to disk.");

    private static readonly ConfigEntry<LogLevel> ConfigDiskLoggingDisplayedLevel = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "LogLevels",
     LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
     "Only displays the specified log levels in the disk log output.");

    private static readonly ConfigEntry<bool> ConfigDiskLoggingInstantFlushing = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "InstantFlushing",
     false,
     """
     If true, instantly writes any received log entries to disk.
     This incurs a major performance hit if a lot of log messages are being written, however it is really useful for debugging crashes.
     """);

    private static readonly ConfigEntry<int> ConfigDiskLoggingFileLimit = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "ConcurrentFileLimit",
     5,
     """
     The maximum amount of concurrent log files that will be written to disk.
     As one log file is used per open game instance, you may find it necessary to increase this limit when debugging multiple instances at the same time.
     """);

    [GeneratedRegex(@"^[a-zA-Z0-9\._\-]+$")]
    private static partial Regex GetAllowedGuidRegex();

    #endregion
}
