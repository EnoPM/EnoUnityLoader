using System.Collections.Generic;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Extensions;

public static class ModuleDefinitionExtensions
{
    public static TypeDefinition? Resolve(this ModuleDefinition module, string typeFullName)
    {
        return module.ResolveInModule(typeFullName) ?? module.ResolveInReferences(typeFullName);
    }

    public static TypeDefinition? ResolveInModule(this ModuleDefinition module, string typeFullName)
    {
        foreach (var type in module.Types)
        {
            if (type.TryFindNestedType(typeFullName, out var nestedType))
            {
                return nestedType;
            }
        }

        return null;
    }

    public static TypeDefinition? ResolveInReferences(this ModuleDefinition module, string typeFullName)
    {
        foreach (var reference in module.AssemblyReferences)
        {
            try
            {
                var referencedAssembly = module.AssemblyResolver.Resolve(reference);
                var type = referencedAssembly.ResolveInAssembly(typeFullName);
                if (type != null)
                {
                    return type;
                }
            }
            catch (AssemblyResolutionException)
            {
                // Assembly couldn't be resolved, continue to next
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a type from a specific assembly by name.
    /// </summary>
    public static TypeDefinition? ResolveInAssembly(this ModuleDefinition module, string typeFullName, string assemblyName)
    {
        try
        {
            var assemblyNameRef = new AssemblyNameReference(assemblyName, null);
            var assembly = module.AssemblyResolver.Resolve(assemblyNameRef);
            return assembly?.ResolveInAssembly(typeFullName);
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a type by trying to resolve its assembly through the module's resolver.
    /// This method attempts to find the type in any assembly that can be resolved,
    /// including indirect dependencies.
    /// </summary>
    public static TypeDefinition? ResolveResolvable(this ModuleDefinition module, string typeFullName)
    {
        // First try in the module itself
        var inModule = module.ResolveInModule(typeFullName);
        if (inModule != null) return inModule;

        // Then try in direct references
        var inReferences = module.ResolveInReferences(typeFullName);
        if (inReferences != null) return inReferences;

        // Try to resolve the assembly by inferring the assembly name from the type name
        // For types like "System.NotImplementedException", try to resolve "System.Runtime"
        // This is a fallback - the AssemblyResolver should handle this
        var possibleAssemblyName = GetPossibleAssemblyName(typeFullName);
        if (possibleAssemblyName != null)
        {
            try
            {
                var assemblyNameRef = new AssemblyNameReference(possibleAssemblyName, null);
                var assembly = module.AssemblyResolver.Resolve(assemblyNameRef);
                var type = assembly?.ResolveInAssembly(typeFullName);
                if (type != null) return type;
            }
            catch (AssemblyResolutionException)
            {
                // Could not resolve the inferred assembly
            }
        }

        // As a last resort, try common BCL assemblies for System types
        if (typeFullName.StartsWith("System."))
        {
            var bcl = TryResolveBclType(module, typeFullName);
            if (bcl != null) return bcl;
        }

        return null;
    }

    private static string? GetPossibleAssemblyName(string typeFullName)
    {
        // For System types, the assembly is often System.Runtime or similar
        if (typeFullName.StartsWith("System."))
        {
            // Try to infer - common patterns
            if (typeFullName.StartsWith("System.Collections.Generic"))
                return "System.Collections";
            if (typeFullName.StartsWith("System.IO."))
                return "System.IO";
            if (typeFullName.StartsWith("System.Linq."))
                return "System.Linq";
            // Most System types are in System.Runtime
            return "System.Runtime";
        }

        // For non-System types, try the root namespace as assembly name
        var dotIndex = typeFullName.LastIndexOf('.');
        return dotIndex > 0 ? typeFullName[..dotIndex] : null;
    }

    private static TypeDefinition? TryResolveBclType(ModuleDefinition module, string typeFullName)
    {
        var bclAssemblies = new[]
        {
            "System.Runtime",
            "System.Private.CoreLib",
            "mscorlib",
            "netstandard"
        };

        foreach (var asmName in bclAssemblies)
        {
            try
            {
                var assemblyNameRef = new AssemblyNameReference(asmName, null);
                var assembly = module.AssemblyResolver.Resolve(assemblyNameRef);
                var type = assembly?.ResolveInAssembly(typeFullName);
                if (type != null) return type;
            }
            catch (AssemblyResolutionException)
            {
                // Assembly not found, try next
            }
        }

        return null;
    }

    public static List<TypeDefinition> GetAllTypes(this ModuleDefinition module)
    {
        var result = new List<TypeDefinition>();
        foreach (var type in module.Types)
        {
            result.Add(type);
            if (!type.HasNestedTypes) continue;
            result.AddRange(type.NestedTypes);
        }

        return result;
    }
}
