using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Extensions;

public static class AssemblyNameReferenceExtensions
{
    public static bool TryResolveAssemblyName(this AssemblyNameReference nameReference, [NotNullWhen(true)] out AssemblyName? name)
    {
        try
        {
            name = new AssemblyName(nameReference.FullName);
            return true;
        }
        catch
        {
            name = null;
            return false;
        }
    }
}
