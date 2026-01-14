using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnoModLoader.Attributes;
using EnoModLoader.Bootstrap;

namespace EnoModLoader.Contract;

/// <summary>
///     Data class that represents information about a loadable ModLoader plugin.
///     Contains all metadata and additional info required for plugin loading by the chainloader.
/// </summary>
public class PluginInfo : ICacheable
{
    /// <summary>
    ///     General metadata about a plugin.
    /// </summary>
    public ModInfosAttribute? Metadata { get; internal set; }

    /// <summary>
    ///     Collection of <see cref="ModProcessAttribute" /> attributes that describe what processes the plugin can run on.
    /// </summary>
    public IEnumerable<ModProcessAttribute> Processes { get; internal set; } = [];

    /// <summary>
    ///     Collection of <see cref="ModDependencyAttribute" /> attributes that describe what plugins this plugin depends on.
    /// </summary>
    public IEnumerable<ModDependencyAttribute> Dependencies { get; internal set; } = [];

    /// <summary>
    ///     Collection of <see cref="ModIncompatibilityAttribute" /> attributes that describe what plugins this plugin
    ///     is incompatible with.
    /// </summary>
    public IEnumerable<ModIncompatibilityAttribute> Incompatibilities { get; internal set; } = [];

    /// <summary>
    ///     File path to the plugin DLL
    /// </summary>
    public string Location { get; internal set; } = string.Empty;

    /// <summary>
    ///     Instance of the plugin that represents this info. NULL if no plugin is instantiated from info (yet)
    /// </summary>
    public object? Instance { get; internal set; }

    /// <summary>
    ///     Full name of the plugin type.
    /// </summary>
    public string TypeName { get; internal set; } = string.Empty;

    internal Version TargettedModLoaderVersion { get; set; } = new();

    void ICacheable.Save(BinaryWriter bw)
    {
        bw.Write(TypeName);
        bw.Write(Location);

        bw.Write(Metadata?.Guid ?? string.Empty);
        bw.Write(Metadata?.Name ?? string.Empty);
        bw.Write(Metadata?.Version?.ToString() ?? "0.0.0");

        var processList = Processes.ToList();
        bw.Write(processList.Count);
        foreach (var bepInProcess in processList)
            bw.Write(bepInProcess.ProcessName);

        var depList = Dependencies.ToList();
        bw.Write(depList.Count);
        foreach (var bepInDependency in depList)
            ((ICacheable)bepInDependency).Save(bw);

        var incList = Incompatibilities.ToList();
        bw.Write(incList.Count);
        foreach (var bepInIncompatibility in incList)
            ((ICacheable)bepInIncompatibility).Save(bw);

        // Ensure version has 4 components for serialization
        var version = TargettedModLoaderVersion;
        var normalizedVersion = new Version(
            version.Major >= 0 ? version.Major : 0,
            version.Minor >= 0 ? version.Minor : 0,
            version.Build >= 0 ? version.Build : 0,
            version.Revision >= 0 ? version.Revision : 0);
        bw.Write(normalizedVersion.ToString(4));
    }

    void ICacheable.Load(BinaryReader br)
    {
        TypeName = br.ReadString();
        Location = br.ReadString();

        Metadata = new ModInfosAttribute(br.ReadString(), br.ReadString(), br.ReadString());

        var processListCount = br.ReadInt32();
        var processList = new List<ModProcessAttribute>(processListCount);
        for (var i = 0; i < processListCount; i++)
            processList.Add(new ModProcessAttribute(br.ReadString()));
        Processes = processList;

        var depCount = br.ReadInt32();
        var depList = new List<ModDependencyAttribute>(depCount);
        for (var i = 0; i < depCount; i++)
        {
            var dep = new ModDependencyAttribute("");
            ((ICacheable)dep).Load(br);
            depList.Add(dep);
        }

        Dependencies = depList;

        var incCount = br.ReadInt32();
        var incList = new List<ModIncompatibilityAttribute>(incCount);
        for (var i = 0; i < incCount; i++)
        {
            var inc = new ModIncompatibilityAttribute("");
            ((ICacheable)inc).Load(br);
            incList.Add(inc);
        }

        Incompatibilities = incList;

        TargettedModLoaderVersion = new Version(br.ReadString());
    }

    /// <inheritdoc />
    public override string ToString() => $"{Metadata?.Name} {Metadata?.Version}";
}
