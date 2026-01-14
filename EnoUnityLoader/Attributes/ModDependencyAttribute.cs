using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnoModLoader.Bootstrap;
using EnoModLoader.Contract;
using Mono.Cecil;

namespace EnoModLoader.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModDependencyAttribute : Attribute, ICacheable
{
    public string DependencyGuid { get; private set; }
    public DependencyFlags Flags { get; private set; }
    public SemanticVersioning.Range? VersionRange { get; private set; }

    public ModDependencyAttribute(string guid, DependencyFlags flags = DependencyFlags.HardDependency)
    {
        DependencyGuid = guid;
        Flags = flags;
    }

    public ModDependencyAttribute(string guid, string version) : this(guid)
    {
        VersionRange = SemanticVersioning.Range.Parse(version);
    }
    
    void ICacheable.Save(BinaryWriter bw)
    {
        bw.Write(DependencyGuid);
        bw.Write((int)Flags);
        bw.Write(VersionRange?.ToString() ?? string.Empty);
    }

    void ICacheable.Load(BinaryReader br)
    {
        DependencyGuid = br.ReadString();
        Flags = (DependencyFlags)br.ReadInt32();

        var versionRange = br.ReadString();
        VersionRange = versionRange == string.Empty ? null : SemanticVersioning.Range.Parse(versionRange);
    }

    internal static IEnumerable<ModDependencyAttribute> FromCecilType(TypeDefinition typeDefinition)
    {
        var attributes = MetadataHelper.GetCustomAttributes<ModDependencyAttribute>(typeDefinition, true);
        return attributes.Select(attribute =>
        {
            var dependencyGuid = (string)attribute.ConstructorArguments[0].Value;
            var secondArgument = attribute.ConstructorArguments[1].Value;
            if (secondArgument is string minVersion)
            {
                return new ModDependencyAttribute(dependencyGuid, minVersion);
            }
            return new ModDependencyAttribute(dependencyGuid, (DependencyFlags)secondArgument);
        }).ToList();
    }
    
    [Flags]
    public enum DependencyFlags
    {
        /// <summary>
        /// The plugin has a hard dependency on the referenced plugin, and will not run without it.
        /// </summary>
        HardDependency = 1,

        /// <summary>
        /// This plugin has a soft dependency on the referenced plugin, and is able to run without it.
        /// </summary>
        SoftDependency = 2
    }
}