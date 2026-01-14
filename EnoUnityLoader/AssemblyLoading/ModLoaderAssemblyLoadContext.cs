using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace EnoUnityLoader.AssemblyLoading;

/// <summary>
/// Custom AssemblyLoadContext for ModLoader plugin loading.
/// Replaces AppDomain-based assembly loading from .NET Framework.
/// </summary>
public sealed class ModLoaderAssemblyLoadContext : AssemblyLoadContext
{
    private readonly List<string> _searchDirectories = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Creates a new ModLoaderAssemblyLoadContext.
    /// </summary>
    /// <param name="name">Name of the context for debugging purposes.</param>
    /// <param name="isCollectible">Whether assemblies can be unloaded.</param>
    public ModLoaderAssemblyLoadContext(string name, bool isCollectible = false)
        : base(name, isCollectible)
    {
    }

    /// <summary>
    /// Default shared instance of the ModLoader assembly load context.
    /// </summary>
    public new static ModLoaderAssemblyLoadContext Default { get; } = new("ModLoader", false);

    /// <summary>
    /// Gets the list of directories to search for assemblies.
    /// </summary>
    public IReadOnlyList<string> SearchDirectories
    {
        get
        {
            lock (_lock)
            {
                return [.. _searchDirectories];
            }
        }
    }

    /// <summary>
    /// Adds a directory to search for assemblies.
    /// </summary>
    /// <param name="directory">Directory path to add.</param>
    public void AddSearchDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        lock (_lock)
        {
            if (!_searchDirectories.Contains(directory))
                _searchDirectories.Add(directory);
        }
    }

    /// <summary>
    /// Removes a directory from the search list.
    /// </summary>
    /// <param name="directory">Directory path to remove.</param>
    public void RemoveSearchDirectory(string directory)
    {
        lock (_lock)
        {
            _searchDirectories.Remove(directory);
        }
    }

    /// <summary>
    /// Loads an assembly from the specified path.
    /// </summary>
    /// <param name="path">Full path to the assembly file.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly LoadFromPath(string path)
    {
        return LoadFromAssemblyPath(Path.GetFullPath(path));
    }

    /// <summary>
    /// Attempts to resolve an assembly by name from the search directories.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to find the assembly in our search directories
        lock (_lock)
        {
            foreach (var directory in _searchDirectories)
            {
                var assemblyPath = Path.Combine(directory, $"{assemblyName.Name}.dll");
                if (File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                // Also try .exe extension
                assemblyPath = Path.Combine(directory, $"{assemblyName.Name}.exe");
                if (File.Exists(assemblyPath))
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }
            }
        }

        // Fall back to default resolution
        return null;
    }

    /// <summary>
    /// Gets all DLL files from all search directories.
    /// Used for Cecil assembly resolution (replaces AppDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).
    /// </summary>
    /// <returns>Semicolon-separated list of all DLL paths in search directories.</returns>
    public string GetTrustedPlatformAssemblies()
    {
        var assemblies = new List<string>();

        lock (_lock)
        {
            foreach (var directory in _searchDirectories)
            {
                if (Directory.Exists(directory))
                {
                    assemblies.AddRange(Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly));
                }
            }
        }

        return string.Join(Path.PathSeparator.ToString(), assemblies);
    }
}
