using System.Collections.Generic;
using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Common;
using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Processor that handles serialization of MonoBehaviour fields for IL2CPP.
/// Converts serialized fields to Il2CppField wrappers and generates deserialization code.
/// </summary>
public sealed class SerializationProcessor : BaseMonoBehaviourProcessor
{
    private readonly Loadable<SerializationContext> _context;

    public SerializationProcessor(MonoBehaviourContext context) : base(context)
    {
        _context = new Loadable<SerializationContext>(CreateSerializationContext);
    }

    public override void Process()
    {
        var serializedFields = UnityUtility.GetSerializedFields(Context.ProcessingType, Context.InteropTypes);
        if (serializedFields.Count == 0)
        {
            return;
        }

        ProcessSerializedFieldInterops(serializedFields);

        // Used to implement 'ISerializationCallbackReceiver' interface in 'Il2CppRegistrationProcessor'
        Context.InteropSummary.SerializedMonoBehaviourFullNames.Add(Context.ProcessingType.FullName);
    }

    private void ProcessSerializedFieldInterops(List<FieldDefinition> serializedFields)
    {
        var serializedFieldData = new List<SerializedFieldGenerationData>();
        foreach (var field in serializedFields)
        {
            var context = new SerializedFieldContext(_context.Value, field);
            var processor = new Il2CppSerializedFieldProcessor(context);
            processor.Process();
            serializedFieldData.Add(processor.ToSerializedFieldData());
        }

        if (!_context.HasValue || !_context.Value.DeserializationMethod.HasValue) return;
        var il = _context.Value.DeserializationMethod.Value.Body.GetILProcessor();
        il.Emit(OpCodes.Ret);

        CreateSerializationInterfaceMethods();
        AddDeserializeMethodCall();
    }

    private void AddDeserializeMethodCall()
    {
        if (Context.UseUnitySerializationInterface) return;
        var awakeMethod = FindOrCreateAwakeMethod(out var parentAwakeMethod);
        var il = awakeMethod.Body.GetILProcessor();
        il.Prepend([
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Call, Context.ProcessingModule.ImportReference(_context.Value.DeserializationMethod.Value))
        ]);
        if (parentAwakeMethod == null) return;
        var hasParentAwakeMethodCall = awakeMethod.Body.Instructions
            .Any(x => x.OpCode == OpCodes.Call && x.Operand is MethodReference xMethod &&
                      xMethod.FullName == parentAwakeMethod.FullName);
        if (hasParentAwakeMethodCall) return;
        il.Prepend([
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Call, Context.ProcessingModule.ImportReference(parentAwakeMethod))
        ]);
    }

    private MethodDefinition FindOrCreateAwakeMethod(out MethodDefinition? parentAwakeMethodResult)
    {
        if (!Context.ProcessingType.TryFindNearestMethod(NearestAwakeMethodFinder, out var nearestAwakeMethod))
        {
            // No awake method found in parent or in current class
            var newAwakeMethod = CreateEmptyAwakeMethod(MethodAttributes.Private);
            Context.ProcessingType.Methods.Add(newAwakeMethod);
            parentAwakeMethodResult = null;
            return newAwakeMethod;
        }

        if (nearestAwakeMethod.DeclaringType.FullName == Context.ProcessingType.FullName)
        {
            // Awake method found in current class
            var parentAwakeMethod = EnsureParentAwakeMethodAccess();
            if (parentAwakeMethod != null)
            {
                // Parent class has a generated Awake method
                nearestAwakeMethod.IsPrivate = false;
                nearestAwakeMethod.IsPublic = parentAwakeMethod.IsPublic;
                nearestAwakeMethod.IsFamily = parentAwakeMethod.IsFamily;
                nearestAwakeMethod.IsVirtual = true;
                nearestAwakeMethod.IsHideBySig = true;
            }

            parentAwakeMethodResult = parentAwakeMethod;
            return nearestAwakeMethod;
        }

        // Awake method found in parent class
        if (nearestAwakeMethod.IsPrivate)
        {
            nearestAwakeMethod.IsPrivate = false;
            nearestAwakeMethod.IsFamily = true;
        }

        if (!nearestAwakeMethod.IsVirtual)
        {
            nearestAwakeMethod.IsVirtual = true;
        }

        var attributes = MethodAttributes.Virtual | MethodAttributes.HideBySig;
        if (nearestAwakeMethod.IsFamily)
        {
            attributes |= MethodAttributes.Family;
        }

        var awakeMethod = CreateEmptyAwakeMethod(attributes);
        Context.ProcessingType.Methods.Add(awakeMethod);

        parentAwakeMethodResult = nearestAwakeMethod;

        return awakeMethod;
    }

    private MethodDefinition? EnsureParentAwakeMethodAccess()
    {
        if (!Context.ProcessingType.TryFindNearestMethod(NearestParentAwakeMethodFinder, out var parentAwakeMethod))
        {
            return null;
        }

        if (parentAwakeMethod.IsPrivate)
        {
            parentAwakeMethod.IsPrivate = false;
            parentAwakeMethod.IsFamily = true;
        }

        if (!parentAwakeMethod.IsVirtual)
        {
            parentAwakeMethod.IsVirtual = true;
        }

        return parentAwakeMethod;
    }

    private static bool NearestAwakeMethodFinder(MethodDefinition method)
    {
        return method.Name == "Awake" && !method.HasParameters;
    }

    private bool NearestParentAwakeMethodFinder(MethodDefinition method)
    {
        return method.DeclaringType.FullName != Context.ProcessingType.FullName && NearestAwakeMethodFinder(method);
    }

    private MethodDefinition CreateEmptyAwakeMethod(MethodAttributes attributes)
    {
        var awakeMethod = new MethodDefinition("Awake", attributes, Context.ProcessingModule.TypeSystem.Void);

        var il = awakeMethod.Body.GetILProcessor();
        il.Emit(OpCodes.Ret);

        return awakeMethod;
    }

    private void CreateSerializationInterfaceMethods()
    {
        if (!Context.UseUnitySerializationInterface) return;
        CreateBeforeSerializationMethod();
        CreateAfterDeserializationMethod();
    }

    private void CreateBeforeSerializationMethod()
    {
        var method = new MethodDefinition(
            "OnBeforeSerialize",
            MethodAttributes.Public,
            Context.ProcessingModule.TypeSystem.Void);

        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ret);

        Context.ProcessingType.Methods.Add(method);
    }

    private void CreateAfterDeserializationMethod()
    {
        if (_context.HasValue && _context.Value.DeserializationMethod.HasValue) return;
        _context.Value.DeserializationMethod.Load();
    }

    private MethodDefinition CreateDeserializationMethod()
    {
        var methodName = $"__AutoInterop_{Context.ProcessingType.Name}_AfterDeserializeMethod";
        if (Context.UseUnitySerializationInterface)
        {
            methodName = "OnAfterDeserialize";
        }

        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Private,
            Context.ProcessingModule.TypeSystem.Void
        );

        Context.ProcessingType.Methods.Add(method);

        return method;
    }

    private SerializationContext CreateSerializationContext()
    {
        return new SerializationContext(
            Context,
            new Loadable<MethodDefinition>(CreateDeserializationMethod)
        );
    }
}
