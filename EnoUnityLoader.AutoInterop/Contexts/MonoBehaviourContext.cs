using EnoUnityLoader.AutoInterop.Core.Interfaces;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Context for processing a MonoBehaviour type.
/// </summary>
public class MonoBehaviourContext : ModuleContext, ITypeProcessorContext
{
    public TypeDefinition ProcessingType { get; }

    public MonoBehaviourContext(ModuleContext context, TypeDefinition processingType)
        : base(context)
    {
        ProcessingType = processingType;
    }

    protected MonoBehaviourContext(MonoBehaviourContext context)
        : this(context, context.ProcessingType)
    {
    }
}
