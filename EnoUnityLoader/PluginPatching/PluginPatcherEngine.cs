using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EnoUnityLoader.AssemblyLoading;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Bootstrap;
using EnoUnityLoader.Configuration;
using EnoUnityLoader.Contract;
using EnoUnityLoader.Logging;
using Mono.Cecil;

namespace EnoUnityLoader.PluginPatching;

/// <summary>
/// Engine that discovers, loads, and executes plugin patchers.
/// Manages caching of patched plugin assemblies.
/// </summary>
public class PluginPatcherEngine : IDisposable
{
    private ManualLogSource Logger { get; }

    /// <summary>
    /// Context containing all loaded patchers and cache configuration.
    /// </summary>
    public PluginPatcherContext Context { get; } = new();

    /// <summary>
    /// Cache entries indexed by plugin location.
    /// </summary>
    private Dictionary<string, PatchedPluginCacheEntry> CacheEntries { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cached patched assemblies loaded into memory, indexed by plugin location.
    /// </summary>
    private Dictionary<string, Assembly> LoadedPatchedAssemblies { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hash of each patcher assembly, indexed by patcher GUID.
    /// </summary>
    private Dictionary<string, string> PatcherHashes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new plugin patcher engine instance.
    /// </summary>
    public PluginPatcherEngine()
    {
        Logger = Logging.Logger.CreateLogSource("PluginPatcherEngine");
        Context.CachePath = Path.Combine(Paths.CachePath, "patched_plugins", Paths.ProcessName);
        Context.CacheIndexPath = Path.Combine(Context.CachePath, "cache_index.dat");
    }

    /// <summary>
    /// Loads all plugin patchers from the specified directory.
    /// </summary>
    /// <param name="directory">Directory to search for patcher DLLs.</param>
    public void LoadPatchersFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Logger.LogDebug($"Plugin patcher directory does not exist: {directory}");
            return;
        }

        var patchers = TypeLoader.FindPluginTypes(directory, ToPatcherMetadata, HasPluginPatchers, "plugin_patchers");

        foreach (var kvp in patchers)
        {
            var assemblyPath = kvp.Key;
            var patcherMetadataList = kvp.Value;

            if (patcherMetadataList.Count == 0)
                continue;

            try
            {
                var assembly = ModLoaderAssemblyLoadContext.Default.LoadFromPath(assemblyPath);

                foreach (var metadata in patcherMetadataList)
                {
                    try
                    {
                        var type = assembly.GetType(metadata.TypeName);
                        if (type == null)
                        {
                            Logger.LogWarning($"Failed to find type [{metadata.TypeName}] in assembly [{assembly.FullName}]");
                            continue;
                        }

                        var instance = (BasePluginPatcher?)Activator.CreateInstance(type);
                        if (instance == null)
                        {
                            Logger.LogWarning($"Failed to create instance of [{metadata.TypeName}]");
                            continue;
                        }

                        instance.Context = Context;
                        instance.AssemblyPath = assemblyPath;
                        Context.Patchers.Add(instance);

                        // Compute and cache patcher hash
                        using var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        PatcherHashes[instance.Info.GUID] = Utility.HashStream(fs);

                        Logger.LogInfo($"Loaded plugin patcher: {instance.Info.Name} v{instance.Info.Version}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to load patcher [{metadata.TypeName}]: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load patcher assembly [{assemblyPath}]: {ex}");
            }
        }

        Logger.LogInfo($"Loaded {Context.Patchers.Count} plugin patcher(s)");

        // Load cache index
        LoadCacheIndex();
    }

    private PluginPatcherMetadata? ToPatcherMetadata(TypeDefinition type, string assemblyPath)
    {
        if (type.IsInterface || type.IsAbstract && !type.IsSealed)
            return null;

        try
        {
            if (!type.IsSubtypeOf(typeof(BasePluginPatcher)))
                return null;
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }

        var metadata = PluginPatcherInfoAttribute.FromCecilType(type);
        if (metadata == null)
        {
            Logger.LogWarning($"Skipping type [{type.FullName}] as no PluginPatcherInfo attribute is specified");
            return null;
        }

        if (string.IsNullOrEmpty(metadata.GUID))
        {
            Logger.LogWarning($"Skipping type [{type.FullName}] because its GUID is empty");
            return null;
        }

        return new PluginPatcherMetadata { TypeName = type.FullName };
    }

    private static bool HasPluginPatchers(AssemblyDefinition assembly)
    {
        var currentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        return assembly.MainModule.AssemblyReferences.Any(r => r.Name == currentAssemblyName)
               && assembly.MainModule.GetTypeReferences().Any(r => r.FullName == typeof(BasePluginPatcher).FullName);
    }

    /// <summary>
    /// Attempts to get a cached patched assembly for the specified plugin.
    /// </summary>
    /// <param name="pluginInfo">The plugin to check.</param>
    /// <param name="assembly">The cached assembly if found and valid.</param>
    /// <returns>True if a valid cached assembly was found.</returns>
    public bool TryGetCachedAssembly(PluginInfo pluginInfo, out Assembly? assembly)
    {
        assembly = null;

        if (!ConfigEnablePluginPatcherCache.Value)
            return false;

        // Check if already loaded
        if (LoadedPatchedAssemblies.TryGetValue(pluginInfo.Location, out assembly))
            return true;

        // Check if cache entry exists
        if (!CacheEntries.TryGetValue(pluginInfo.Location, out var cacheEntry))
            return false;

        // Load assembly with Cecil to check targeting patchers
        var originalBytes = File.ReadAllBytes(pluginInfo.Location);
        using var originalStream = new MemoryStream(originalBytes);
        var originalHash = Utility.HashStream(originalStream);
        originalStream.Position = 0;

        using var assemblyDefinition = AssemblyDefinition.ReadAssembly(originalStream, TypeLoader.ReaderParameters);

        // Get targeting patchers
        var targetingPatchers = GetTargetingPatchers(pluginInfo, assemblyDefinition);
        if (targetingPatchers.Count == 0)
            return false;

        // Validate cache
        if (!ValidateCacheEntry(pluginInfo, cacheEntry, targetingPatchers, originalHash))
        {
            // Remove invalid cache entry
            CacheEntries.Remove(pluginInfo.Location);
            TryDeleteCachedFile(cacheEntry.CachedAssemblyPath);
            return false;
        }

        // Load cached assembly
        if (!File.Exists(cacheEntry.CachedAssemblyPath))
            return false;

        try
        {
            assembly = ModLoaderAssemblyLoadContext.Default.LoadFromPath(cacheEntry.CachedAssemblyPath);
            LoadedPatchedAssemblies[pluginInfo.Location] = assembly;
            Logger.LogDebug($"Loaded cached patched plugin: {pluginInfo.Metadata?.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load cached assembly for [{pluginInfo.Metadata?.Name}]: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Patches a plugin and returns the loaded assembly.
    /// </summary>
    /// <param name="pluginInfo">The plugin to patch.</param>
    /// <returns>The patched assembly, or null if no patchers target this plugin.</returns>
    public Assembly? PatchPlugin(PluginInfo pluginInfo)
    {
        // Read original assembly
        var originalBytes = File.ReadAllBytes(pluginInfo.Location);
        using var originalStream = new MemoryStream(originalBytes);
        var originalHash = Utility.HashStream(originalStream);
        originalStream.Position = 0;

        // Load assembly with Cecil
        using var assemblyDefinition = AssemblyDefinition.ReadAssembly(originalStream, TypeLoader.ReaderParameters);

        // Get targeting patchers (needs the assembly for ShouldPatch)
        var targetingPatchers = GetTargetingPatchers(pluginInfo, assemblyDefinition);
        if (targetingPatchers.Count == 0)
            return null;

        Logger.LogInfo($"Patching plugin: {pluginInfo.Metadata?.Name} with {targetingPatchers.Count} patcher(s)");

        // Initialize patchers
        foreach (var patcher in targetingPatchers)
        {
            try
            {
                patcher.Initialize();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize patcher [{patcher.Info.Name}]: {ex}");
            }
        }

        // Apply patches
        var appliedPatchers = new List<string>();
        foreach (var patcher in targetingPatchers)
        {
            try
            {
                Logger.LogDebug($"Applying patcher [{patcher.Info.Name}] to [{pluginInfo.Metadata?.Name}]");
                patcher.Patch(pluginInfo.Metadata!, assemblyDefinition);
                appliedPatchers.Add(patcher.Info.GUID);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Patcher [{patcher.Info.Name}] failed on [{pluginInfo.Metadata?.Name}]: {ex}");
            }
        }

        // Finalize patchers
        foreach (var patcher in targetingPatchers)
        {
            try
            {
                patcher.Finalizer();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to finalize patcher [{patcher.Info.Name}]: {ex}");
            }
        }

        // Write patched assembly
        using var patchedStream = new MemoryStream();
        assemblyDefinition.Write(patchedStream);
        var patchedBytes = patchedStream.ToArray();

        // Save to cache if enabled
        if (ConfigEnablePluginPatcherCache.Value)
        {
            SaveToCache(pluginInfo, patchedBytes, originalHash, appliedPatchers, targetingPatchers);
        }

        // Load assembly
        var assembly = Assembly.Load(patchedBytes);
        LoadedPatchedAssemblies[pluginInfo.Location] = assembly;

        // Dump if configured
        if (ConfigDumpPatchedPlugins.Value)
        {
            DumpPatchedPlugin(pluginInfo, patchedBytes);
        }

        Logger.LogInfo($"Patched plugin: {pluginInfo.Metadata?.Name}");
        return assembly;
    }

    /// <summary>
    /// Gets all patchers that target the specified plugin.
    /// </summary>
    /// <param name="pluginInfo">The plugin info.</param>
    /// <param name="assembly">The Cecil AssemblyDefinition of the plugin.</param>
    /// <returns>List of patchers that should patch this plugin.</returns>
    public List<BasePluginPatcher> GetTargetingPatchers(PluginInfo pluginInfo, AssemblyDefinition assembly)
    {
        if (pluginInfo.Metadata == null)
            return [];

        return Context.Patchers.Where(p =>
        {
            try
            {
                return p.ShouldPatch(pluginInfo.Metadata, assembly);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in ShouldPatch for [{p.Info.Name}]: {ex}");
                return false;
            }
        }).ToList();
    }

    private bool ValidateCacheEntry(PluginInfo pluginInfo, PatchedPluginCacheEntry entry, List<BasePluginPatcher> targetingPatchers, string originalHash)
    {
        // Check original location
        if (!entry.OriginalLocation.Equals(pluginInfo.Location, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check original assembly hash
        if (entry.OriginalAssemblyHash != originalHash)
        {
            Logger.LogDebug($"Cache invalid for [{pluginInfo.Metadata?.Name}]: original assembly changed");
            return false;
        }

        // Check patchers hash
        var currentPatchersHash = ComputePatchersHash(targetingPatchers);
        if (entry.PatchersHash != currentPatchersHash)
        {
            Logger.LogDebug($"Cache invalid for [{pluginInfo.Metadata?.Name}]: patchers changed");
            return false;
        }

        return true;
    }

    private string ComputePatchersHash(List<BasePluginPatcher> patchers)
    {
        var sortedPatchers = patchers.OrderBy(p => p.Info.GUID).ToList();
        var sb = new StringBuilder();

        foreach (var patcher in sortedPatchers)
        {
            sb.Append(patcher.Info.GUID);
            sb.Append(patcher.Info.Version?.ToString() ?? "0.0.0");
            if (PatcherHashes.TryGetValue(patcher.Info.GUID, out var hash))
                sb.Append(hash);
        }

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        return Utility.HashStream(ms);
    }

    private string SaveToCache(PluginInfo pluginInfo, byte[] patchedBytes, string originalHash, List<string> appliedPatchers, List<BasePluginPatcher> targetingPatchers)
    {
        try
        {
            // Ensure cache directory exists
            var pluginCacheDir = Path.Combine(Context.CachePath, pluginInfo.Metadata?.Guid ?? "unknown");
            if (!Directory.Exists(pluginCacheDir))
                Directory.CreateDirectory(pluginCacheDir);

            // Save patched assembly
            var cachedPath = Path.Combine(pluginCacheDir, "patched.dll");
            File.WriteAllBytes(cachedPath, patchedBytes);

            // Create cache entry
            var entry = new PatchedPluginCacheEntry
            {
                PluginGuid = pluginInfo.Metadata?.Guid ?? string.Empty,
                OriginalLocation = pluginInfo.Location,
                OriginalAssemblyHash = originalHash,
                PatchersHash = ComputePatchersHash(targetingPatchers),
                AppliedPatchers = appliedPatchers,
                CachedAssemblyPath = cachedPath
            };

            CacheEntries[pluginInfo.Location] = entry;
            SaveCacheIndex();

            Logger.LogDebug($"Cached patched plugin: {pluginInfo.Metadata?.Name} at {cachedPath}");
            return cachedPath;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to cache patched plugin [{pluginInfo.Metadata?.Name}]: {ex.Message}");
            return string.Empty;
        }
    }

    private void DumpPatchedPlugin(PluginInfo pluginInfo, byte[] patchedBytes)
    {
        try
        {
            var dumpDir = Path.Combine(Paths.ModLoaderRootPath, "DumpedPatchedPlugins", Paths.ProcessName);
            if (!Directory.Exists(dumpDir))
                Directory.CreateDirectory(dumpDir);

            var dumpPath = Path.Combine(dumpDir, Path.GetFileName(pluginInfo.Location));
            File.WriteAllBytes(dumpPath, patchedBytes);
            Logger.LogDebug($"Dumped patched plugin to: {dumpPath}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to dump patched plugin: {ex.Message}");
        }
    }

    private void LoadCacheIndex()
    {
        if (!File.Exists(Context.CacheIndexPath))
            return;

        try
        {
            using var br = new BinaryReader(File.OpenRead(Context.CacheIndexPath));
            var count = br.ReadInt32();

            for (var i = 0; i < count; i++)
            {
                var entry = new PatchedPluginCacheEntry();
                entry.Load(br);
                CacheEntries[entry.OriginalLocation] = entry;
            }

            Logger.LogDebug($"Loaded {count} cache entries from index");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load cache index: {ex.Message}");
            CacheEntries.Clear();
        }
    }

    private void SaveCacheIndex()
    {
        try
        {
            if (!Directory.Exists(Context.CachePath))
                Directory.CreateDirectory(Context.CachePath);

            using var bw = new BinaryWriter(File.Create(Context.CacheIndexPath));
            bw.Write(CacheEntries.Count);

            foreach (var entry in CacheEntries.Values)
                entry.Save(bw);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to save cache index: {ex.Message}");
        }
    }

    private void TryDeleteCachedFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);

            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                Directory.Delete(dir);
        }
        catch { }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Context.Patchers.Clear();
        CacheEntries.Clear();
        LoadedPatchedAssemblies.Clear();
        PatcherHashes.Clear();
    }

    #region Config

    private static readonly ConfigEntry<bool> ConfigEnablePluginPatching = ConfigFile.CoreConfig.Bind(
        "PluginPatching", "Enabled",
        true,
        "Enable/disable the plugin patching system.");

    private static readonly ConfigEntry<bool> ConfigEnablePluginPatcherCache = ConfigFile.CoreConfig.Bind(
        "PluginPatching", "EnableCache",
        true,
        "Enable/disable caching of patched plugin assemblies.\nWhen enabled, patched plugins are cached to disk and reused if neither the plugin nor the patchers have changed.");

    private static readonly ConfigEntry<bool> ConfigDumpPatchedPlugins = ConfigFile.CoreConfig.Bind(
        "PluginPatching", "DumpPatchedPlugins",
        false,
        "Save patched plugin assemblies to ModLoader/DumpedPatchedPlugins for debugging.");

    /// <summary>
    /// Gets whether plugin patching is enabled.
    /// </summary>
    public static bool IsEnabled => ConfigEnablePluginPatching.Value;

    #endregion
}
