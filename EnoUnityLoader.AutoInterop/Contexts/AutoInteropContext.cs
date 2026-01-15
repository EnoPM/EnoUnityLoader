using System.Collections.Generic;
using EnoUnityLoader.AutoInterop.Core.Interfaces;
using EnoUnityLoader.Logging;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Root context for AutoInterop processing.
/// Contains shared configuration for the patching process.
/// </summary>
public class AutoInteropContext : IContext
{
    public HashSet<string> AssemblyFilePaths { get; }
    public InteropSummary InteropSummary { get; }
    public bool UseUnitySerializationInterface { get; }
    public string? UnityProjectDirectoryPath { get; }
    public ManualLogSource Logger { get; }

    public AutoInteropContext(
        HashSet<string> assemblyFilePaths,
        ManualLogSource logger,
        bool useUnitySerializationInterface = false,
        string? unityProjectDirectoryPath = null)
    {
        AssemblyFilePaths = assemblyFilePaths;
        Logger = logger;
        UseUnitySerializationInterface = useUnitySerializationInterface;
        UnityProjectDirectoryPath = unityProjectDirectoryPath;
        InteropSummary = new InteropSummary();
    }

    protected AutoInteropContext(AutoInteropContext context)
        : this(context.AssemblyFilePaths, context.Logger,
               context.UseUnitySerializationInterface, context.UnityProjectDirectoryPath)
    {
    }
}
