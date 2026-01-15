using System;
using System.Linq;
using EnoUnityLoader.Contract;
using Mono.Cecil;
using Version = SemanticVersioning.Version;

namespace EnoUnityLoader.Attributes;

/// <summary>
/// Attribute that identifies a class as a plugin patcher and provides metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PluginPatcherInfoAttribute : Attribute
{
    /// <param name="guid">The unique identifier of the patcher. Should not change between versions.</param>
    /// <param name="name">The user friendly name of the patcher.</param>
    /// <param name="version">The specific version of the patcher.</param>
    public PluginPatcherInfoAttribute(string guid, string name, string version)
    {
        GUID = guid;
        Name = name;
        Version = TryParseLongVersion(version);
    }

    /// <summary>
    /// The unique identifier of the patcher. Should not change between versions.
    /// </summary>
    public string GUID { get; protected set; }

    /// <summary>
    /// The user friendly name of the patcher.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// The specific version of the patcher.
    /// </summary>
    public Version? Version { get; protected set; }

    private static Version? TryParseLongVersion(string version)
    {
        if (Version.TryParse(version, out var v))
            return v;

        try
        {
            var longVersion = new System.Version(version);
            return new Version(longVersion.Major, longVersion.Minor,
                               longVersion.Build != -1 ? longVersion.Build : 0);
        }
        catch { }

        return null;
    }

    internal static PluginPatcherInfoAttribute? FromCecilType(TypeDefinition td)
    {
        var attr = MetadataHelper.GetCustomAttributes<PluginPatcherInfoAttribute>(td, false).FirstOrDefault();

        if (attr == null)
            return null;

        return new PluginPatcherInfoAttribute((string)attr.ConstructorArguments[0].Value,
                                               (string)attr.ConstructorArguments[1].Value,
                                               (string)attr.ConstructorArguments[2].Value);
    }

    internal static PluginPatcherInfoAttribute? FromType(Type type)
    {
        var attributes = type.GetCustomAttributes(typeof(PluginPatcherInfoAttribute), false);

        if (attributes.Length == 0)
            return null;

        return (PluginPatcherInfoAttribute)attributes[0];
    }
}
