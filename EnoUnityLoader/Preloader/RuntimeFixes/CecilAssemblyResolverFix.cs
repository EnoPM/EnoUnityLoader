using System;
using System.IO;
using Mono.Cecil;

namespace EnoUnityLoader.Preloader.RuntimeFixes;

/// <summary>
/// Configures Cecil's assembly resolver to find .NET 10 runtime assemblies.
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
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        // Get the runtime directory from the CoreLib assembly location
        // This is the most reliable way to find where .NET 10 runtime assemblies are
        var coreLibPath = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(coreLibPath))
        {
            RuntimeDirectory = Path.GetDirectoryName(coreLibPath);
        }

        // Fallback: try to find it from dotnet shared directory
        if (string.IsNullOrEmpty(RuntimeDirectory) || !Directory.Exists(RuntimeDirectory))
        {
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (string.IsNullOrEmpty(dotnetRoot))
            {
                // Try common locations
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var possiblePaths = new[]
                {
                    Path.Combine(userProfile, ".dotnet"),
                    @"C:\Program Files\dotnet",
                    @"C:\Program Files (x86)\dotnet"
                };

                foreach (var path in possiblePaths)
                {
                    var sharedPath = Path.Combine(path, "shared", "Microsoft.NETCore.App");
                    if (Directory.Exists(sharedPath))
                    {
                        // Find the highest version directory
                        var versions = Directory.GetDirectories(sharedPath);
                        foreach (var versionDir in versions)
                        {
                            if (Path.GetFileName(versionDir).StartsWith("10."))
                            {
                                RuntimeDirectory = versionDir;
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(RuntimeDirectory))
                            break;
                    }
                }
            }
        }

        // Configure environment variable for any tools that respect it
        if (!string.IsNullOrEmpty(RuntimeDirectory))
        {
            Environment.SetEnvironmentVariable("MONOMOD_DETOURER_CECIL_PATH", RuntimeDirectory);
        }
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
