using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Cecil.Utils;
using EnoUnityLoader.AutoInterop.Common;
using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Core.Processors;
using EnoUnityLoader.AutoInterop.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Processor for individual serialized fields.
/// Converts fields to Il2CppField wrappers and generates deserialization IL code.
/// </summary>
public class Il2CppSerializedFieldProcessor : BaseFieldProcessor<SerializedFieldContext>
{
    private readonly string _serializedFieldName;
    private readonly Loadable<TypeDefinition> _interopFieldType;
    private readonly Loadable<TypeReference> _serializedFieldTypeReference;
    private readonly Loadable<FieldDefinition> _serializedField;
    private readonly Loadable<bool> _isPluginMonoBehaviourFieldType;
    private FieldDefinition UsableField => Context.ProcessingField;

    public Il2CppSerializedFieldProcessor(SerializedFieldContext context) : base(context)
    {
        _isPluginMonoBehaviourFieldType = new Loadable<bool>(CheckIfItsPluginMonoBehaviourFieldType);
        _serializedFieldName = UsableField.Name;
        _interopFieldType = Il2CppInteropUtility.GetSerializedFieldInteropType(UsableField, Context);
        _serializedFieldTypeReference = new Loadable<TypeReference>(CreateSerializedFieldTypeReference);
        _serializedField = new Loadable<FieldDefinition>(CreateSerializedField);
    }

    public override void Process()
    {
        RenameUsedField();
        _serializedField.Load();
        AddDeserializationInstruction();
    }

    public SerializedFieldGenerationData ToSerializedFieldData()
    {
        return new SerializedFieldGenerationData(UsableField, _serializedField.Value);
    }

    private void RenameUsedField()
    {
        var oldFullName = UsableField.FullName;
        var newFieldName = $"__AutoInterop_UsableField_{_serializedFieldName}";
        UsableField.Name = newFieldName;

        // Update references within the current assembly
        foreach (var module in Context.ProcessingAssembly.Modules)
        {
            foreach (var type in module.Types)
            {
                UpdateFieldReferencesInType(type, oldFullName, module);
            }
        }
    }

    private void UpdateFieldReferencesInType(TypeDefinition type, string oldFullName, ModuleDefinition module)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            while (method.HasFieldUsage(oldFullName))
            {
                var il = method.Body.GetILProcessor();
                var instructionToReplace = method.Body.Instructions
                    .First(x => MethodsUtility.IsFieldRelatedInstruction(x)
                    && x.Operand is FieldReference reference
                    && reference.FullName == oldFullName);
                var newInstruction = il.Create(
                    instructionToReplace.OpCode,
                    module.ImportReference(UsableField));
                il.Replace(instructionToReplace, newInstruction);
                Context.Logger.LogDebug($"Replacing field reference in {method.FullName}");
            }
        }

        // Process nested types
        foreach (var nestedType in type.NestedTypes)
        {
            UpdateFieldReferencesInType(nestedType, oldFullName, module);
        }
    }

    private void AddDeserializationInstruction()
    {
        var il = Context.DeserializationMethod.Value.Body.GetILProcessor();
        var interopGetMethod = Context.ProcessingModule.ImportReference(_interopFieldType.Value.Methods.First(x => x.Name == "Get"));
        if (_interopFieldType.Value.HasGenericParameters)
        {
            interopGetMethod.DeclaringType = Context.ProcessingModule.ImportReference(
                _interopFieldType.Value.MakeGenericInstanceType(_isPluginMonoBehaviourFieldType.Value ? Context.InteropTypes.GameObjectType.Value : UsableField.FieldType)
            );
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _serializedField.Value);
        il.Emit(OpCodes.Callvirt, interopGetMethod);
        if (_isPluginMonoBehaviourFieldType.Value)
        {
            var getComponentMethod = new GenericInstanceMethod(Context.ProcessingModule.ImportReference(Context.InteropTypes.GameObjectGetComponentMethod.Value));
            getComponentMethod.GenericArguments.Add(UsableField.FieldType);

            il.Emit(OpCodes.Callvirt, getComponentMethod);
        }
        il.Emit(OpCodes.Stfld, UsableField);
    }

    private FieldDefinition CreateSerializedField()
    {
        var field = new FieldDefinition(
            _serializedFieldName,
            FieldAttributes.Public,
            Context.ProcessingModule.ImportReference(_serializedFieldTypeReference.Value)
        );

        Context.ProcessingType.Fields.Add(field);
        Context.Logger.LogDebug($"Adding field <{field.Name}> to {Context.ProcessingType.Name}.");

        return field;
    }

    private TypeReference CreateSerializedFieldTypeReference()
    {
        var interopType = _interopFieldType.Value;
        if (interopType.HasGenericParameters)
        {
            var genericParameter = _isPluginMonoBehaviourFieldType.Value ? Context.InteropTypes.GameObjectType.Value : UsableField.FieldType;
            return interopType.CreateGenericInstanceType(genericParameter);
        }
        return interopType;
    }

    private bool CheckIfItsPluginMonoBehaviourFieldType() => Il2CppInteropUtility.IsPluginMonoBehaviourFieldType(UsableField, Context);
}
