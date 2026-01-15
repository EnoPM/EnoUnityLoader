using System.Collections.Generic;

namespace EnoUnityLoader.AutoInterop.Contexts;

public sealed class InteropSummary
{
    public HashSet<string> SerializedMonoBehaviourFullNames { get; } = [];
    public HashSet<string> RegisteredMonoBehaviourFullNames { get; } = [];
    public HashSet<string> UnityProjectGeneratedFilePaths { get; } = [];
}
