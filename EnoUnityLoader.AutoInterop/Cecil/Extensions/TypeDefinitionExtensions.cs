using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Utils;
using EnoUnityLoader.Logging;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Extensions;

public static class TypeDefinitionExtensions
{
    private static readonly HashSet<string> UnresolvedWarnedAncestors = [];
    private static ManualLogSource? _logger;

    internal static void SetLogger(ManualLogSource logger) => _logger = logger;

    public static bool TryFindNestedType(
        this TypeDefinition type,
        string fullName,
        [MaybeNullWhen(false)] out TypeDefinition result)
    {
        if (type.FullName == fullName)
        {
            result = type;
            return true;
        }

        foreach (var nested in type.NestedTypes)
        {
            if (!nested.TryFindNestedType(fullName, out var nestedResult)) continue;
            result = nestedResult;
            return true;
        }
        result = null;
        return false;
    }

    public static bool IsAssignableFrom(this TypeDefinition source, LoadableType target)
    {
        return target.Value.IsAssignableFrom(source);
    }

    private static bool IsAssignableFrom(this TypeDefinition source, TypeDefinition target) => target.IsAssignableTo(source);

    public static bool IsAssignableTo<T>(this TypeDefinition source) where T : class
    {
        return source.IsAssignableTo(typeof(T).FullName ?? typeof(T).Name);
    }

    public static bool IsAssignableTo(this TypeDefinition source, LoadableType target)
    {
        return source.IsAssignableTo(target.FullName);
    }

    public static bool IsAssignableTo(this TypeDefinition source, TypeDefinition target)
    {
        return source.IsAssignableTo(target.FullName);
    }

    public static bool IsAssignableTo(this TypeDefinition source, string targetTypeFullName)
    {
        while (source.BaseType != null && source.BaseType.FullName != targetTypeFullName)
        {
            if (source.BaseType == null)
            {
                return false;
            }

            if (source.BaseType.FullName == targetTypeFullName)
            {
                return true;
            }

            try
            {
                var ancestor = source.BaseType.Resolve();
                if (ancestor == null)
                {
                    return false;
                }
                source = ancestor;
            }
            catch (Exception ex)
            {
                if (UnresolvedWarnedAncestors.Add(source.BaseType.FullName))
                {
                    _logger?.LogWarning($"Unresolvable ancestor type '{source.BaseType.FullName}'. {ex.Message}");
                }
                return false;
            }
        }

        return source.BaseType != null && source.BaseType.FullName == targetTypeFullName;
    }

    public static bool TryFindNearestMethod(
        this TypeDefinition type,
        Func<MethodDefinition, bool> methodFilter,
        [MaybeNullWhen(false)] out MethodDefinition method)
    {
        var baseType = type;

        while (baseType != null)
        {
            var nearestMethod = baseType.Methods.FirstOrDefault(methodFilter);

            if (nearestMethod != null)
            {
                method = nearestMethod;
                return true;
            }

            if (baseType.BaseType == null)
            {
                method = null;
                return false;
            }

            try
            {
                baseType = baseType.BaseType.Resolve();
            }
            catch (Exception ex)
            {
                if (UnresolvedWarnedAncestors.Add(baseType.BaseType.FullName))
                {
                    _logger?.LogWarning($"Unresolvable ancestor type '{baseType.BaseType.FullName}'. {ex.Message}");
                }
                baseType = null;
            }
        }

        method = null;
        return false;
    }
}
