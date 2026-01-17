using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using EnoUnityLoader.Console;
using EnoUnityLoader.Il2Cpp.RuntimeFixes;
using EnoUnityLoader.Ipc;
using EnoUnityLoader.Ipc.Messages;
using EnoUnityLoader.Logging;
using EnoUnityLoader.Preloader;
using EnoUnityLoader.Preloader.Patching;
using EnoUnityLoader.Preloader.RuntimeFixes;
using EnoUnityLoader.Unity;
using AssemblyPatcher = EnoUnityLoader.Preloader.Patching.AssemblyPatcher;
using LogLevel = EnoUnityLoader.Logging.LogLevel;

namespace EnoUnityLoader.Il2Cpp;

/// <summary>
///     IL2CPP preloader.
/// </summary>
public static class Preloader
{
    private static PreloaderConsoleListener? PreloaderLog { get; set; }

    internal static ManualLogSource Log => PreloaderLogger.Log;

    // TODO: This is not needed, maybe remove? (Instance is saved in IL2CPPChainloader itself)
    private static IL2CPPChainLoader? Chainloader { get; set; }

    /// <summary>
    ///     Assemblies that were patched and loaded by the preloader.
    ///     The chainloader should use these instead of loading from disk.
    /// </summary>
    public static Dictionary<string, Assembly>? PatchedAssemblies { get; private set; }

    /// <summary>
    ///     Runs the IL2CPP preloader.
    /// </summary>
    public static void Run()
    {
        try
        {
            HarmonyBackendFix.Initialize();
            ConsoleSetOutFix.Apply();
            UnityInfo.Initialize(Paths.ExecutablePath, Paths.GameDataPath);

            ConsoleManager.Initialize(false, true);

            PreloaderLog = new PreloaderConsoleListener();
            Logger.Listeners.Add(PreloaderLog);

            if (ConsoleManager.ConsoleEnabled)
            {
                ConsoleManager.CreateConsole();
                Logger.Listeners.Add(new ConsoleLogListener());
            }

            RedirectStdErrFix.Apply();

            // Launch UI and connect via IPC
            _ = IpcManager.InitializeAsync(Paths.ModLoaderAssemblyDirectory);
            IpcManager.SendStatus(LoaderStatus.Initializing);
            IpcManager.SendProgress("Initializing", "Starting mod loader...");

            ChainloaderLogHelper.PrintLogInfo(Log);

            Logger.Log(LogLevel.Info, $"Running under Unity {UnityInfo.Version}");
            Logger.Log(LogLevel.Info, $"Runtime version: {Environment.Version}");
            Logger.Log(LogLevel.Info, $"Runtime information: {RuntimeInformation.FrameworkDescription}");

            Logger.Log(LogLevel.Debug, $"Game executable path: {Paths.ExecutablePath}");
            Logger.Log(LogLevel.Debug, $"Interop assembly directory: {Il2CppInteropManager.IL2CPPInteropAssemblyPath}");
            Logger.Log(LogLevel.Debug, $"ModLoader root path: {Paths.ModLoaderRootPath}");

            if (PlatformHelper.Is(Platform.Wine) && !Environment.Is64BitProcess)
            {
                if (!NativeLibrary.TryGetExport(NativeLibrary.Load("ntdll"), "RtlRestoreContext", out _))
                {
                    Logger.Log(LogLevel.Warning,
                               "Your wine version doesn't support CoreCLR properly, expect crashes! Upgrade to wine 7.16 or higher.");
                }
            }

            IpcManager.SendProgress("Initializing", "Setting up IL2CPP interop...");

            NativeLibrary.SetDllImportResolver(typeof(Il2CppInterop.Runtime.IL2CPP).Assembly, DllImportResolver);

            IpcManager.SendStatus(LoaderStatus.GeneratingInterop);
            IpcManager.SendProgress("Generating Interop", "Initializing IL2CPP interop assemblies...");

            Il2CppInteropManager.Initialize();

            IpcManager.SendProgress("Loading Assemblies", "Discovering patchers and assemblies...");

            using (var assemblyPatcher = new AssemblyPatcher((data, _) => Assembly.Load(data)))
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                // Load interop assemblies and plugin assemblies for patching (separately tracked)
                assemblyPatcher.LoadAssemblyDirectories(PatcherTarget.Interop, Il2CppInteropManager.IL2CPPInteropAssemblyPath);
                assemblyPatcher.LoadAssemblyDirectories(PatcherTarget.Plugins, Paths.PluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                IpcManager.SendProgress("Patching Assemblies", "Applying patches...");

                assemblyPatcher.PatchAndLoad();

                // Store loaded assemblies for chainloader to use
                PatchedAssemblies = assemblyPatcher.PatcherContext.LoadedAssemblies;
            }


            Logger.Listeners.Remove(PreloaderLog);

            IpcManager.SendStatus(LoaderStatus.LoadingMods);
            IpcManager.SendProgress("Loading Mods", "Initializing chainloader...");

            Chainloader = new IL2CPPChainLoader();

            Chainloader.Initialize();
        }
        catch (Exception ex)
        {
            Log.Log(LogLevel.Fatal, ex);
            IpcManager.SendReady(false, ex.Message);

            throw;
        }
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "GameAssembly")
        {
            return NativeLibrary.Load(Il2CppInteropManager.GameAssemblyPath, assembly, searchPath);
        }

        return IntPtr.Zero;
    }
}
