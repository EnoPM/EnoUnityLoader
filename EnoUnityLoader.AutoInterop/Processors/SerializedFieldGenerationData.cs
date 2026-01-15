using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Data about a serialized field for code generation.
/// </summary>
public class SerializedFieldGenerationData
{
    public FieldDefinition UsableField { get; }
    public FieldDefinition SerializedField { get; }

    public SerializedFieldGenerationData(FieldDefinition usableField, FieldDefinition serializedField)
    {
        UsableField = usableField;
        SerializedField = serializedField;
    }
}
