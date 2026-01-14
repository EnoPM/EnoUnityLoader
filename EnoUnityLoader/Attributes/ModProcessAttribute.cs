using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnoUnityLoader.Bootstrap;
using EnoUnityLoader.Contract;
using Mono.Cecil;

namespace EnoUnityLoader.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModProcessAttribute(string processName) : Attribute, ICacheable
{
    public string ProcessName { get; private set; } = processName;

    void ICacheable.Save(BinaryWriter bw) => bw.Write(ProcessName);

    void ICacheable.Load(BinaryReader br) => ProcessName = br.ReadString();

    internal static IEnumerable<ModProcessAttribute> FromCecilType(TypeDefinition typeDefinition)
    {
        var attributes = MetadataHelper.GetCustomAttributes<ModProcessAttribute>(typeDefinition, true);
        return attributes.Select(attribute =>
        {
            var processName = (string)attribute.ConstructorArguments[0].Value;
            return new ModProcessAttribute(processName);
        }).ToList();
    }
}