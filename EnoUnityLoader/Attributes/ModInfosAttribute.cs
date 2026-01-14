using System;
using System.Linq;
using EnoUnityLoader.Contract;
using Mono.Cecil;

namespace EnoUnityLoader.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModInfosAttribute(string guid, string name, string version) : Attribute
{
    public string Guid { get; } = guid;
    public string Name { get; } = name;
    public SemanticVersioning.Version? Version { get; } = TryParseLongVersion(version);

    private static SemanticVersioning.Version? TryParseLongVersion(string version)
    {
        if (SemanticVersioning.Version.TryParse(version, out var result))
        {
            return result;
        }

        try
        {
            var longVersion = new System.Version(version);
            return new SemanticVersioning.Version(longVersion.Major, longVersion.Minor,
                longVersion.Build != -1 ? longVersion.Build : 0);
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    internal static ModInfosAttribute? FromCecilType(TypeDefinition type)
    {
        var attribute = MetadataHelper.GetCustomAttributes<ModInfosAttribute>(type, false)
            .FirstOrDefault();

        if (attribute == null)
        {
            return null;
        }

        return new ModInfosAttribute(
            (string)attribute.ConstructorArguments[0].Value,
            (string)attribute.ConstructorArguments[1].Value,
            (string)attribute.ConstructorArguments[2].Value
        );
    }
}