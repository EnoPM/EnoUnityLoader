using System;

namespace EnoUnityLoader.AutoInterop.Cecil.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class CecilResolveAttribute : Attribute
{
    public readonly string FullName;
    public readonly ResolverContext Context;
    public readonly string? AssemblyName;

    public CecilResolveAttribute(string fullName, ResolverContext context = ResolverContext.All, string? assemblyName = null)
    {
        FullName = fullName;
        Context = context;
        AssemblyName = assemblyName;
    }
}
