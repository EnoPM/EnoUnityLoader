using EnoUnityLoader.AutoInterop.Common;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Context for serialization processing.
/// Contains the deserialization method being built.
/// </summary>
public class SerializationContext : MonoBehaviourContext
{
    public Loadable<MethodDefinition> DeserializationMethod { get; }

    public SerializationContext(MonoBehaviourContext context, Loadable<MethodDefinition> deserializationMethod)
        : base(context)
    {
        DeserializationMethod = deserializationMethod;
    }

    protected SerializationContext(SerializationContext context)
        : this(context, context.DeserializationMethod)
    {
    }
}
