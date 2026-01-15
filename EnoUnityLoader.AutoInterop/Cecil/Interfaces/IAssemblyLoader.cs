using System.Diagnostics.CodeAnalysis;
using System.IO;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Interfaces;

public interface IAssemblyLoader
{
    public IAssemblyDependencyManager Dependencies { get; }

    public AssemblyDefinition? ResolveAssembly(AssemblyNameReference nameReference);
    public AssemblyDefinition Load(string assemblyPath);
    public AssemblyDefinition Load(Stream assemblyStream);

    public void LoadDependencies();

    public bool TryResolveUnreferenced(
        ModuleDefinition module,
        string typeFullName,
        [MaybeNullWhen(false)] out TypeDefinition resolvedType
    );
}
