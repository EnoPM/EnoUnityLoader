using System.Collections.Generic;
using System.IO;
using EnoUnityLoader.Bootstrap;

namespace EnoUnityLoader.PluginPatching;

/// <summary>
/// Cache entry for a patched plugin assembly.
/// </summary>
internal class PatchedPluginCacheEntry : ICacheable
{
    /// <summary>
    /// GUID of the plugin.
    /// </summary>
    public string PluginGuid { get; set; } = string.Empty;

    /// <summary>
    /// Original location of the plugin assembly.
    /// </summary>
    public string OriginalLocation { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the original plugin assembly.
    /// </summary>
    public string OriginalAssemblyHash { get; set; } = string.Empty;

    /// <summary>
    /// Combined hash of all patchers targeting this plugin.
    /// </summary>
    public string PatchersHash { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of patcher GUIDs that were applied.
    /// </summary>
    public List<string> AppliedPatchers { get; set; } = [];

    /// <summary>
    /// Path to the cached patched assembly file.
    /// </summary>
    public string CachedAssemblyPath { get; set; } = string.Empty;

    /// <inheritdoc />
    public void Save(BinaryWriter bw)
    {
        bw.Write(PluginGuid);
        bw.Write(OriginalLocation);
        bw.Write(OriginalAssemblyHash);
        bw.Write(PatchersHash);

        bw.Write(AppliedPatchers.Count);
        foreach (var patcherGuid in AppliedPatchers)
            bw.Write(patcherGuid);

        bw.Write(CachedAssemblyPath);
    }

    /// <inheritdoc />
    public void Load(BinaryReader br)
    {
        PluginGuid = br.ReadString();
        OriginalLocation = br.ReadString();
        OriginalAssemblyHash = br.ReadString();
        PatchersHash = br.ReadString();

        var patcherCount = br.ReadInt32();
        AppliedPatchers = new List<string>(patcherCount);
        for (var i = 0; i < patcherCount; i++)
            AppliedPatchers.Add(br.ReadString());

        CachedAssemblyPath = br.ReadString();
    }
}
