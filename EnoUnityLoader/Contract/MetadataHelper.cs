using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EnoModLoader.Attributes;
using Mono.Cecil;

namespace EnoModLoader.Contract;

/// <summary>
/// Helper class to use for retrieving metadata about a plugin, defined as attributes.
/// </summary>
public static class MetadataHelper
{
    internal static IEnumerable<CustomAttribute> GetCustomAttributes<T>(TypeDefinition td, bool inherit)
        where T : Attribute
    {
        var result = new List<CustomAttribute>();
        var type = typeof(T);
        TypeDefinition? currentType = td;

        while (currentType != null && currentType.FullName != "System.Object")
        {
            result.AddRange(currentType.CustomAttributes.Where(ca => ca.AttributeType.FullName == type.FullName));
            if (!inherit)
                break;
            currentType = currentType.BaseType?.Resolve();
        }

        return result;
    }

    /// <summary>
    /// Retrieves the BepInPlugin metadata from a plugin type.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The BepInPlugin metadata of the plugin type.</returns>
    public static ModInfosAttribute? GetMetadata(Type pluginType)
    {
        var attributes = pluginType.GetCustomAttributes(typeof(ModInfosAttribute), false);

        if (attributes.Length == 0)
            return null;

        return (ModInfosAttribute)attributes[0];
    }

    /// <summary>
    /// Retrieves the BepInPlugin metadata from a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The BepInPlugin metadata of the plugin instance.</returns>
    public static ModInfosAttribute? GetMetadata(object plugin) => GetMetadata(plugin.GetType());

    /// <summary>
    /// Gets the specified attributes of a type, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The attributes of the type, if existing.</returns>
    public static T[] GetAttributes<T>(Type pluginType) where T : Attribute =>
        (T[])pluginType.GetCustomAttributes(typeof(T), true);

    /// <summary>
    /// Gets the specified attributes of an assembly, if they exist.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <returns>The attributes of the type, if existing.</returns>
    public static T[] GetAttributes<T>(Assembly assembly) where T : Attribute =>
        (T[])assembly.GetCustomAttributes(typeof(T), true);

    /// <summary>
    /// Gets the specified attributes of an instance, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The attributes of the instance, if existing.</returns>
    public static IEnumerable<T> GetAttributes<T>(object plugin) where T : Attribute =>
        GetAttributes<T>(plugin.GetType());

    /// <summary>
    /// Gets the specified attributes of a reflection metadata type, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="member">The reflection metadata instance.</param>
    /// <returns>The attributes of the instance, if existing.</returns>
    public static T[] GetAttributes<T>(MemberInfo member) where T : Attribute =>
        (T[])member.GetCustomAttributes(typeof(T), true);

    /// <summary>
    /// Retrieves the dependencies of the specified plugin type.
    /// </summary>
    /// <param name="plugin">The plugin type.</param>
    /// <returns>A list of all plugin types that the specified plugin type depends upon.</returns>
    public static IEnumerable<ModDependencyAttribute> GetDependencies(Type plugin) =>
        plugin.GetCustomAttributes(typeof(ModDependencyAttribute), true).Cast<ModDependencyAttribute>();
}