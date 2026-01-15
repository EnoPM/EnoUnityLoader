using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Core.Interfaces;

public interface IFieldProcessorContext : ITypeProcessorContext
{
    public FieldDefinition ProcessingField { get; }
}
