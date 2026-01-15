using System.IO;
using EnoUnityLoader.Bootstrap;

namespace EnoUnityLoader.PluginPatching;

/// <summary>
/// Cacheable metadata for a plugin patcher discovered during type loading.
/// </summary>
internal class PluginPatcherMetadata : ICacheable
{
    /// <summary>
    /// Full type name of the patcher.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <inheritdoc />
    public void Save(BinaryWriter bw) => bw.Write(TypeName);

    /// <inheritdoc />
    public void Load(BinaryReader br) => TypeName = br.ReadString();
}
