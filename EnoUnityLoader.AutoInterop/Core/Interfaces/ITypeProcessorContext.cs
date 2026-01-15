using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Core.Interfaces;

public interface ITypeProcessorContext : IModuleProcessorContext
{
    public TypeDefinition ProcessingType { get; }
}
