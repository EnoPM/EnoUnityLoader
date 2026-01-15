using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Interfaces;

public interface IDependencyFile
{
    public string Path { get; }
    public bool IsLoaded { get; }
    public bool IsAvailable { get; }
    public bool CanBeLoaded { get; }
    public AssemblyDefinition? LoadedAssembly { get; set; }

    public void Load(IAssemblyLoader loader);
}
