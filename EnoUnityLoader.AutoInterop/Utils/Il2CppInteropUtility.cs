using System;
using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Cecil.Utils;
using EnoUnityLoader.AutoInterop.Contexts;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Utils;

internal static class Il2CppInteropUtility
{
    internal static LoadableType GetSerializedFieldInteropType(FieldDefinition field, SerializedFieldContext context)
    {
        var type = field.Module.ResolveResolvable(field.FieldType.FullName);
        if (type == null)
        {
            throw new Exception($"Unable to resolve field type {field.FieldType.FullName}");
        }
        if (type.FullName == field.Module.TypeSystem.String.FullName)
        {
            return context.InteropTypes.Il2CppStringField;
        }
        if (type.IsAssignableTo(context.InteropTypes.UnityEngineObject))
        {
            return context.InteropTypes.Il2CppReferenceField;
        }
        return context.InteropTypes.Il2CppValueField;
    }

    public static bool IsPluginMonoBehaviourFieldType(FieldDefinition field, SerializationContext context)
    {
        var fieldType = field.Module.ResolveResolvable(field.FieldType.FullName);
        if (fieldType == null)
        {
            throw new Exception($"Unable to resolve field type {field.FieldType.FullName}");
        }
        if (!fieldType.IsAssignableTo(context.InteropTypes.MonoBehaviour))
        {
            return false;
        }
        return context.AssemblyFilePaths.Any(x => x == fieldType.Module.FileName);
    }
}
