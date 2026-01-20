using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Processors;
using EnoUnityLoader.AutoInterop.Utils;
using EnoUnityLoader.Bootstrap;
using EnoUnityLoader.Il2Cpp;
using EnoUnityLoader.PluginPatching;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop;

[PluginPatcherInfo("pm.eno.interop.auto", "AutoInterop", "1.0.0")]
public sealed class AutoInterop : BasePluginPatcher
{
    internal static AutoInterop Instance { get; private set; } = null!;

    public AutoInterop()
    {
        Instance = this;
    }

    public override bool ShouldPatch(ModInfosAttribute modInfo, AssemblyDefinition assembly)
    {
        return assembly.MainModule.Types
            .Any(x => x.IsAssignableTo<BasePlugin>());
    }

    public override void Patch(ModInfosAttribute modInfo, AssemblyDefinition assembly)
    {
        Log.LogMessage($"Processing AutoInterop to '{modInfo.Guid}' v{modInfo.Version?.ToString() ?? "0.0.0"}...");

        // Create the context for this assembly - use assembly's own resolver
        var assemblyFilePaths = new HashSet<string> { assembly.MainModule.FileName };
        var context = new AutoInteropContext(assemblyFilePaths, Log, useUnitySerializationInterface: false);

        // Create module context
        var moduleContext = new ModuleContext(context, assembly);

        // Find MonoBehaviour types in this assembly
        var monoBehaviourTypes = UnityUtility.GetMonoBehaviourTypes(
            assembly.MainModule,
            moduleContext.InteropTypes);

        Log.LogInfo($"Found {monoBehaviourTypes.Count} MonoBehaviour types in '{modInfo.Name}'.");

        // Process library dependencies FIRST so their types are registered before plugin types
        // This is important when plugin types inherit from library types
        var cacheDirectory = GetPluginCacheDirectory();
        ProcessLibraryDependencies(moduleContext, cacheDirectory);

        // Process each MonoBehaviour type from the main plugin
        foreach (var monoBehaviourType in monoBehaviourTypes)
        {
            var monoBehaviourContext = new MonoBehaviourContext(moduleContext, monoBehaviourType);
            var processor = new MonoBehaviourProcessor(monoBehaviourContext);
            processor.Process();

            Log.LogDebug($"Processed MonoBehaviour: {monoBehaviourType.FullName}");
        }

        // Generate runtime infrastructure if needed
        if (context.InteropSummary.RegisteredMonoBehaviourFullNames.Count > 0)
        {
            moduleContext.GeneratedRuntime.GenerateRuntimeInfrastructure();
            Log.LogInfo($"Generated runtime infrastructure for {context.InteropSummary.RegisteredMonoBehaviourFullNames.Count} registered types.");
        }

        Log.LogMessage($"AutoInterop completed for '{modInfo.Name}'.");
    }

    private string? GetPluginCacheDirectory()
    {
        var pluginLocation = Context?.CurrentPluginLocation;
        if (string.IsNullOrEmpty(Context?.CachePath) || string.IsNullOrEmpty(pluginLocation))
            return null;

        // Use plugin folder name to mirror source structure
        var pluginDir = Path.GetDirectoryName(pluginLocation);
        var pluginFolderName = !string.IsNullOrEmpty(pluginDir)
            ? Path.GetFileName(pluginDir)
            : "unknown";

        var cacheDir = Path.Combine(Context.CachePath, pluginFolderName);
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);

        return cacheDir;
    }

    private void ProcessLibraryDependencies(ModuleContext pluginModuleContext, string? cacheDirectory)
    {
        var pluginLocation = Context?.CurrentPluginLocation;
        if (string.IsNullOrEmpty(pluginLocation))
        {
            Log.LogDebug("Plugin location is null or empty, skipping library discovery.");
            return;
        }

        var pluginDirectory = Path.GetDirectoryName(pluginLocation);
        if (string.IsNullOrEmpty(pluginDirectory))
        {
            Log.LogDebug("Plugin directory is null or empty, skipping library discovery.");
            return;
        }

        var pluginFileName = Path.GetFileName(pluginLocation);
        Log.LogDebug($"Searching for libraries in: {pluginDirectory}");

        // Find all DLL files in the same directory (excluding the plugin itself)
        var libraryFiles = Directory.GetFiles(pluginDirectory, "*.dll")
            .Where(f => !Path.GetFileName(f).Equals(pluginFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Log.LogDebug($"Found {libraryFiles.Count} potential library file(s) in plugin directory.");

        foreach (var libraryPath in libraryFiles)
        {
            ProcessLibraryAssembly(libraryPath, pluginModuleContext, cacheDirectory);
        }
    }

    private void ProcessLibraryAssembly(string libraryPath, ModuleContext pluginModuleContext, string? cacheDirectory)
    {
        try
        {
            using var libraryAssembly = AssemblyDefinition.ReadAssembly(libraryPath, TypeLoader.ReaderParameters);

            // Skip if this is a plugin (has BasePlugin)
            if (libraryAssembly.MainModule.Types.Any(x => x.IsAssignableTo<BasePlugin>()))
            {
                return;
            }

            // Create a module context for the library (for type modifications)
            var libraryModuleContext = new ModuleContext(pluginModuleContext, libraryAssembly);

            // Find MonoBehaviour types in this library
            var monoBehaviourTypes = UnityUtility.GetMonoBehaviourTypes(
                libraryAssembly.MainModule,
                libraryModuleContext.InteropTypes);

            if (monoBehaviourTypes.Count == 0)
            {
                return;
            }

            Log.LogInfo($"Found {monoBehaviourTypes.Count} MonoBehaviour types in library '{Path.GetFileName(libraryPath)}'.");

            var modified = false;

            foreach (var monoBehaviourType in monoBehaviourTypes)
            {
                // Create context for type modifications in the library
                var monoBehaviourContext = new MonoBehaviourContext(libraryModuleContext, monoBehaviourType);

                // Process type modifications (IntPtr constructor, etc.)
                var processor = new LibraryMonoBehaviourProcessor(monoBehaviourContext);
                processor.Process();

                // Register the type in the main plugin's RegisterCurrentPlugin method
                var useSerializationInterface = pluginModuleContext.UseUnitySerializationInterface &&
                    pluginModuleContext.InteropSummary.SerializedMonoBehaviourFullNames.Contains(monoBehaviourType.FullName);

                pluginModuleContext.GeneratedRuntime.RegisterExternalType(monoBehaviourType, useSerializationInterface);
                pluginModuleContext.InteropSummary.RegisteredMonoBehaviourFullNames.Add(monoBehaviourType.FullName);

                Log.LogDebug($"Processed library MonoBehaviour: {monoBehaviourType.FullName}");
                modified = true;
            }

            // Save the modified library to cache directory
            if (modified && !string.IsNullOrEmpty(cacheDirectory))
            {
                var libraryFileName = Path.GetFileName(libraryPath);
                var cachedLibraryPath = Path.Combine(cacheDirectory, libraryFileName);
                libraryAssembly.Write(cachedLibraryPath);
                Log.LogInfo($"Saved modified library to cache: {libraryFileName}");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to process library '{Path.GetFileName(libraryPath)}': {ex.Message}");
        }
    }
}
