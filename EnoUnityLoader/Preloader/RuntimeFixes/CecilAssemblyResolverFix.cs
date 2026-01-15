using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace EnoUnityLoader.Preloader.RuntimeFixes;

/// <summary>
/// Configures Cecil's assembly resolver to find .NET 10 runtime assemblies.
/// This is needed because MonoMod/Harmony uses Cecil internally and needs to resolve
/// System.Private.CoreLib and other BCL assemblies when creating dynamic methods.
/// </summary>
public static class CecilAssemblyResolverFix
{
    private static bool _initialized;

    /// <summary>
    /// Gets the .NET runtime directory containing System.Private.CoreLib.dll
    /// </summary>
    public static string? RuntimeDirectory { get; private set; }

    /// <summary>
    /// Initializes the Cecil assembly resolver with .NET 10 runtime paths.
    /// Must be called early in the startup process, before Harmony/MonoMod is used.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        // Get the runtime directory from the CoreLib assembly location
        var coreLibPath = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(coreLibPath))
        {
            RuntimeDirectory = Path.GetDirectoryName(coreLibPath);
        }

        // Fallback: try RuntimeEnvironment
        if (string.IsNullOrEmpty(RuntimeDirectory) || !Directory.Exists(RuntimeDirectory))
        {
            RuntimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        }

        if (string.IsNullOrEmpty(RuntimeDirectory) || !Directory.Exists(RuntimeDirectory))
        {
            return;
        }

        // Hook into AppDomain.AssemblyResolve to help Cecil find BCL assemblies
        // This is needed because MonoMod's Cecil instance doesn't have the runtime directory configured
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        if (string.IsNullOrEmpty(RuntimeDirectory))
            return null;

        var assemblyName = new AssemblyName(args.Name);

        // Only handle BCL assemblies
        if (!IsBclAssembly(assemblyName.Name))
            return null;

        var assemblyPath = Path.Combine(RuntimeDirectory, assemblyName.Name + ".dll");
        if (File.Exists(assemblyPath))
        {
            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                // Ignore load failures
            }
        }

        return null;
    }

    private static bool IsBclAssembly(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.StartsWith("System.") ||
               name == "System" ||
               name == "mscorlib" ||
               name == "netstandard" ||
               name.StartsWith("Microsoft.") ||
               name == "WindowsBase";
    }

    /// <summary>
    /// Creates a pre-configured assembly resolver for Cecil that can find .NET 10 assemblies.
    /// </summary>
    public static DefaultAssemblyResolver CreateConfiguredResolver()
    {
        Initialize();

        var resolver = new DefaultAssemblyResolver();

        if (!string.IsNullOrEmpty(RuntimeDirectory) && Directory.Exists(RuntimeDirectory))
        {
            resolver.AddSearchDirectory(RuntimeDirectory);
        }

        return resolver;
    }

    /// <summary>
    /// Adds .NET 10 runtime directory to an existing resolver.
    /// </summary>
    public static void ConfigureResolver(BaseAssemblyResolver resolver)
    {
        Initialize();

        if (!string.IsNullOrEmpty(RuntimeDirectory) && Directory.Exists(RuntimeDirectory))
        {
            resolver.AddSearchDirectory(RuntimeDirectory);
        }
    }
}
