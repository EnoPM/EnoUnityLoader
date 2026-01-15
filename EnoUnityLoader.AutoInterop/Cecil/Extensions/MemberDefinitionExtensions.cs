using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Utils;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Extensions;

public static class MemberDefinitionExtensions
{
    public static bool HasCustomAttribute(this IMemberDefinition member, LoadableType customAttributeType)
    {
        return member.HasCustomAttribute(customAttributeType.FullName);
    }

    public static bool HasCustomAttribute(this IMemberDefinition member, TypeDefinition customAttributeType)
    {
        return member.HasCustomAttribute(customAttributeType.FullName);
    }

    private static bool HasCustomAttribute(this IMemberDefinition member, string customAttributeTypeFullName)
    {
        return member.CustomAttributes.Any(a => a.AttributeType.FullName == customAttributeTypeFullName);
    }
}
