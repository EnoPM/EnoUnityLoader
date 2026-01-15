using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Cecil.Interfaces;
using EnoUnityLoader.Logging;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Core;

public sealed class AssemblyLoader : IAssemblyLoader, IDisposable
{
    public IAssemblyDependencyManager Dependencies { get; }
    private readonly ReaderParameters _readerParameters;
    private readonly List<string> _dependencyDirectories = [];
    private ManualLogSource? _logger;

    public AssemblyLoader(AssemblyLoader? parent = null)
    {
        var dependencyManager = new AssemblyDependencyManager(this);
        Dependencies = dependencyManager;

        _readerParameters = parent?._readerParameters ?? new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            InMemory = true,
            AssemblyResolver = new AssemblyResolver(this)
        };
    }

    public void AddDependencyDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        if (_dependencyDirectories.Contains(directory)) return;
        _dependencyDirectories.Add(directory);

        foreach (var file in Directory.GetFiles(directory, "*.dll"))
        {
            Dependencies.Files.Add(new DependencyFile(file));
        }
    }

    public void SetLogger(ManualLogSource logger)
    {
        _logger = logger;
        ((AssemblyDependencyManager)Dependencies).SetLogger(logger);
    }

    public AssemblyDefinition? ResolveAssembly(AssemblyNameReference assemblyName)
    {
        if (!assemblyName.TryResolveAssemblyName(out var name))
        {
            _logger?.LogWarning($"Unable to resolve assembly name {assemblyName.FullName}");
            return null;
        }

        return Dependencies.FindLoadedAssembly(name);
    }

    public AssemblyDefinition Load(string assemblyPath)
    {
        var dependency = Dependencies.Files.FirstOrDefault(x => x.Path == assemblyPath);
        if (dependency == null)
        {
            dependency = new DependencyFile(assemblyPath);
            Dependencies.Files.Add(dependency);
            return dependency.LoadedAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);
        }
        if (dependency.IsLoaded)
        {
            return dependency.LoadedAssembly!;
        }
        return AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);
    }

    public AssemblyDefinition Load(Stream assemblyStream)
    {
        return AssemblyDefinition.ReadAssembly(assemblyStream, _readerParameters);
    }

    public void LoadDependencies() => Dependencies.LoadAllFiles();

    public bool TryResolveUnreferenced(
        ModuleDefinition module,
        string typeFullName,
        [NotNullWhen(true)] out TypeDefinition? resolvedType)
    {
        var excludedFiles = CompileExcludedFiles(module);

        return TryResolveUnreferenced(typeFullName, excludedFiles, out resolvedType);
    }

    private List<string> CompileExcludedFiles(ModuleDefinition module)
    {
        var excludedFiles = new List<string>();

        foreach (var reference in module.AssemblyReferences)
        {
            try
            {
                var resolved = module.AssemblyResolver.Resolve(reference);
                var fileNames = resolved.Modules.Select(x => x.FileName.ToString()).ToList();
                excludedFiles.AddRange(fileNames);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        return excludedFiles;
    }

    private bool TryResolveUnreferenced(
        string typeFullName,
        List<string> excludedFiles,
        [NotNullWhen(true)] out TypeDefinition? resolvedType
    )
    {
        resolvedType = Dependencies.FindLoadedType(typeFullName, in excludedFiles);
        return resolvedType != null;
    }

    public void Dispose()
    {
        foreach (var file in Dependencies.Files)
        {
            file.LoadedAssembly?.Dispose();
        }
        Dependencies.Files.Clear();
        _dependencyDirectories.Clear();
    }
}
