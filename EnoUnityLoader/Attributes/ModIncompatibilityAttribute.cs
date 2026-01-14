using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnoUnityLoader.Bootstrap;
using EnoUnityLoader.Contract;
using Mono.Cecil;

namespace EnoUnityLoader.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModIncompatibilityAttribute(string incompatibleGuid) : Attribute, ICacheable
{
    public string IncompatibleGuid { get; private set; } = incompatibleGuid;

    void ICacheable.Save(BinaryWriter bw) => bw.Write(IncompatibleGuid);

    void ICacheable.Load(BinaryReader br) => IncompatibleGuid = br.ReadString();

    internal static IEnumerable<ModIncompatibilityAttribute> FromCecilType(TypeDefinition typeDefinition)
    {
        var attributes = MetadataHelper.GetCustomAttributes<ModIncompatibilityAttribute>(typeDefinition, true);
        return attributes.Select(attribute =>
        {
            var incompatibleGuid = (string)attribute.ConstructorArguments[0].Value;
            return new ModIncompatibilityAttribute(incompatibleGuid);
        }).ToList();
    }
}