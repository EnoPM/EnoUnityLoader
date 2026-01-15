using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Utils;

public static class TypeAttributesUtility
{
    public const TypeAttributes Static = TypeAttributes.Abstract | TypeAttributes.Sealed;
    public const TypeAttributes Internal = TypeAttributes.NotPublic;
}
