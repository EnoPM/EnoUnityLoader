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
        var methodBody = Context.DeserializationMethod.Value.Body;
        var interopGetMethod = Context.ProcessingModule.ImportReference(_interopFieldType.Value.Methods.First(x => x.Name == "Get"));
        if (_interopFieldType.Value.HasGenericParameters)
        {
            interopGetMethod.DeclaringType = Context.ProcessingModule.ImportReference(
                _interopFieldType.Value.MakeGenericInstanceType(_isPluginMonoBehaviourFieldType.Value ? Context.InteropTypes.GameObjectType.Value : UsableField.FieldType)
            );
        }

        // Create a local variable to store the caught exception
        var exceptionType = Context.ProcessingModule.ImportReference(typeof(System.Exception));
        var exceptionLocal = new VariableDefinition(exceptionType);
        methodBody.Variables.Add(exceptionLocal);

        // Get the Exception constructor that takes (string message, Exception innerException)
        var exceptionCtor = Context.ProcessingModule.ImportReference(
            typeof(System.Exception).GetConstructor([typeof(string), typeof(System.Exception)]));

        // Try block start
        var tryStart = il.Create(OpCodes.Ldarg_0);
        il.Append(tryStart);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _serializedField.Value);
        il.Emit(OpCodes.Callvirt, interopGetMethod);
        if (_isPluginMonoBehaviourFieldType.Value)
        {
            // Create local variables for activeSelf state and component
            var boolType = Context.ProcessingModule.ImportReference(typeof(bool));
            var activeSelfLocal = new VariableDefinition(boolType);
            methodBody.Variables.Add(activeSelfLocal);

            var componentLocal = new VariableDefinition(UsableField.FieldType);
            methodBody.Variables.Add(componentLocal);

            // Import the methods we need
            var getActiveSelfMethod = Context.ProcessingModule.ImportReference(Context.InteropTypes.GameObjectGetActiveSelfMethod.Value);
            var setActiveMethod = Context.ProcessingModule.ImportReference(Context.InteropTypes.GameObjectSetActiveMethod.Value);

            var getComponentMethod = new GenericInstanceMethod(Context.ProcessingModule.ImportReference(Context.InteropTypes.GameObjectGetComponentMethod.Value));
            getComponentMethod.GenericArguments.Add(UsableField.FieldType);

            // Stack: [this, GameObject]
            // Save activeSelf state
            il.Emit(OpCodes.Dup);                                            // [this, GameObject, GameObject]
            il.Emit(OpCodes.Callvirt, (MethodReference)getActiveSelfMethod); // [this, GameObject, bool]
            il.Emit(OpCodes.Stloc, activeSelfLocal);                         // [this, GameObject]

            // Set GameObject active to true
            il.Emit(OpCodes.Dup);                                            // [this, GameObject, GameObject]
            il.Emit(OpCodes.Ldc_I4_1);                                       // [this, GameObject, GameObject, true]
            il.Emit(OpCodes.Callvirt, (MethodReference)setActiveMethod);     // [this, GameObject]

            // Call GetComponent and save result
            il.Emit(OpCodes.Dup);                                            // [this, GameObject, GameObject]
            il.Emit(OpCodes.Callvirt, getComponentMethod);                    // [this, GameObject, Component]
            il.Emit(OpCodes.Stloc, componentLocal);                          // [this, GameObject]

            // Restore original activeSelf state
            il.Emit(OpCodes.Ldloc, activeSelfLocal);                         // [this, GameObject, bool]
            il.Emit(OpCodes.Callvirt, (MethodReference)setActiveMethod);     // [this]

            // Load the component for stfld
            il.Emit(OpCodes.Ldloc, componentLocal);                          // [this, Component]
        }
        il.Emit(OpCodes.Stfld, UsableField);

        // Leave try block
        var endOfHandler = il.Create(OpCodes.Nop);
        var leaveInstruction = il.Create(OpCodes.Leave, endOfHandler);
        il.Append(leaveInstruction);

        // Catch block start - exception is on stack
        var catchStart = il.Create(OpCodes.Stloc, exceptionLocal);
        il.Append(catchStart);
        il.Emit(OpCodes.Ldstr, $"Unable to deserialize field '{_serializedFieldName}'");
        il.Emit(OpCodes.Ldloc, exceptionLocal);
        il.Emit(OpCodes.Newobj, exceptionCtor);
        il.Emit(OpCodes.Throw);

        // End of handler
        il.Append(endOfHandler);

        // Add exception handler
        var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = catchStart,
            HandlerStart = catchStart,
            HandlerEnd = endOfHandler,
            CatchType = exceptionType
        };
        methodBody.ExceptionHandlers.Add(handler);
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
