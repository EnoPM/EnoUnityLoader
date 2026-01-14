using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using EnoUnityLoader.AssemblyLoading;
using EnoUnityLoader.Preloader;

namespace EnoUnityLoader.IL2CPP;

internal static class UnityPreloadRunner
{
    public static void PreloaderMain()
    {
        var bepinPath =
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH!)))!;

        PlatformUtils.SetPlatform();

        Paths.SetExecutablePath(EnvVars.DOORSTOP_PROCESS_PATH!, bepinPath, EnvVars.DOORSTOP_MANAGED_FOLDER_DIR!, false,
                                EnvVars.DOORSTOP_DLL_SEARCH_DIRS);

        // Cecil 0.11 requires one to manually set up list of trusted assemblies for assembly resolving
        // The main BCL path
        ModLoaderAssemblyLoadContext.Default.AddCecilPlatformAssemblies(Paths.ManagedPath);
        // The parent path -> .NET has some extra managed DLLs in there
        ModLoaderAssemblyLoadContext.Default.AddCecilPlatformAssemblies(Path.GetDirectoryName(Paths.ManagedPath)!);

        // Register assembly resolver
        AssemblyLoadContext.Default.Resolving += LocalResolve;

        Preloader.Run();
    }

    internal static Assembly? LocalResolve(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var foundAssembly = AssemblyLoadContext.Default.Assemblies
                                     .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

        if (foundAssembly != null)
            return foundAssembly;

        if (Utility.TryResolveDllAssembly(assemblyName, Paths.ModLoaderAssemblyDirectory, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
         || Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
            return foundAssembly;

        return null;
    }
}
