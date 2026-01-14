using System.IO;

namespace EnoUnityLoader.Bootstrap;

/// <summary>
/// Interface for objects that can be cached to a binary format.
/// </summary>
public interface ICacheable
{
    /// <summary>
    /// Serialize the object into a binary format.
    /// </summary>
    /// <param name="bw">The binary writer to serialize to.</param>
    void Save(BinaryWriter bw);

    /// <summary>
    /// Loads the object from binary format.
    /// </summary>
    /// <param name="br">The binary reader to deserialize from.</param>
    void Load(BinaryReader br);
}
