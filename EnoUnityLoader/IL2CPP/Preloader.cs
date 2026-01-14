using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using EnoModLoader.Console;
using EnoModLoader.IL2CPP.RuntimeFixes;
using EnoModLoader.Logging;
using EnoModLoader.Preloader;
using EnoModLoader.Preloader.Patching;
using EnoModLoader.Preloader.RuntimeFixes;
using EnoModLoader.Unity;

namespace EnoModLoader.IL2CPP;

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

            NativeLibrary.SetDllImportResolver(typeof(Il2CppInterop.Runtime.IL2CPP).Assembly, DllImportResolver);

            Il2CppInteropManager.Initialize();

            using (var assemblyPatcher = new AssemblyPatcher((data, _) => Assembly.Load(data)))
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                // Load interop assemblies and plugin assemblies for patching (separately tracked)
                assemblyPatcher.LoadAssemblyDirectories(PatcherTarget.Interop, Il2CppInteropManager.IL2CPPInteropAssemblyPath);
                assemblyPatcher.LoadAssemblyDirectories(PatcherTarget.Plugins, Paths.PluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                assemblyPatcher.PatchAndLoad();

                // Store loaded assemblies for chainloader to use
                PatchedAssemblies = assemblyPatcher.PatcherContext.LoadedAssemblies;
            }


            Logger.Listeners.Remove(PreloaderLog);


            Chainloader = new IL2CPPChainLoader();

            Chainloader.Initialize();
        }
        catch (Exception ex)
        {
            Log.Log(LogLevel.Fatal, ex);

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
