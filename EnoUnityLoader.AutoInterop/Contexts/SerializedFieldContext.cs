using EnoUnityLoader.AutoInterop.Core.Interfaces;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Context for processing a serialized field.
/// </summary>
public class SerializedFieldContext : SerializationContext, IFieldProcessorContext
{
    public FieldDefinition ProcessingField { get; }

    public SerializedFieldContext(SerializationContext context, FieldDefinition processingField)
        : base(context)
    {
        ProcessingField = processingField;
    }

    protected SerializedFieldContext(SerializedFieldContext context)
        : this(context, context.ProcessingField)
    {
    }
}
