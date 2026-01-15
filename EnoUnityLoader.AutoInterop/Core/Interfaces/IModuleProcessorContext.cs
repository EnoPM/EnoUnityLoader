using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Core.Interfaces;

public interface IModuleProcessorContext : IContext
{
    public ModuleDefinition ProcessingModule { get; }
}
