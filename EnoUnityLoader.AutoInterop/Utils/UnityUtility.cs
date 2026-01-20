using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Core.Utils;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Utils;

internal static class UnityUtility
{

    internal static List<TypeDefinition> GetMonoBehaviourTypes(ModuleDefinition module, InteropTypesContext interopTypes)
    {
        var types = module.GetAllTypes()
            .Where(t => IsMonoBehaviour(interopTypes, t))
            .ToList();

        var sorter = new TopologicalSorter<TypeDefinition>(types, CreateDependenciesResolver(interopTypes));

        return sorter.Sort();
    }

    internal static List<FieldDefinition> GetSerializedFields(TypeDefinition type, InteropTypesContext interopTypes)
    {
        return type.Fields
            .Where(f => IsSerializedField(interopTypes, f))
            .ToList();
    }

    private static TopologicalSorter<TypeDefinition>.DependenciesResolver CreateDependenciesResolver(InteropTypesContext interopTypes)
    {
        return type =>
        {
            var dependencies = new List<TypeDefinition>();

            // Only base type dependency matters for IL2CPP registration order
            var baseType = type.BaseType?.Resolve();
            if (baseType != null)
            {
                dependencies.Add(baseType);
            }

            foreach (var field in type.Fields)
            {
                var fieldType = field.FieldType.Resolve();
                if (fieldType.FullName == type.FullName) continue;
                if (!IsMonoBehaviour(interopTypes, fieldType)) continue;
                if (fieldType.Module.Assembly.FullName != type.Module.Assembly.FullName) continue;
                dependencies.Add(fieldType);
            }

            return dependencies;
        };
    }

    private static bool IsSerializedField(InteropTypesContext interopTypes, FieldDefinition field)
    {
        if (field.IsLiteral || field.IsStatic || field.IsFamilyOrAssembly || field.IsInitOnly) return false;
        if (field.HasCustomAttributes)
        {
            if (field.HasCustomAttribute(interopTypes.NonSerializedAttribute))
            {
                return false;
            }
            if (field.HasCustomAttribute(interopTypes.SerializeFieldAttribute))
            {
                return true;
            }
        }
        return field.IsPublic;
    }

    private static bool IsMonoBehaviour(InteropTypesContext interopTypes, TypeDefinition type)
    {
        return type.IsAssignableTo(interopTypes.MonoBehaviour);
    }

    public static string GetUnityEditorGeneratedDirectoryPath(string unityProjectDirectory)
    {
        return Path.Combine(Path.GetFullPath(unityProjectDirectory), "Assets", "AutoInterop", "Generated");
    }
}