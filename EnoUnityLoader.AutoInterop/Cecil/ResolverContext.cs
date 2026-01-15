namespace EnoUnityLoader.AutoInterop.Cecil;

public enum ResolverContext
{
    /// <summary>
    /// Resolve only within the current module.
    /// </summary>
    Internal,

    /// <summary>
    /// Resolve in direct assembly references only.
    /// </summary>
    Referenced,

    /// <summary>
    /// Resolve using the module's resolver for any resolvable assembly.
    /// </summary>
    Referenceable,

    /// <summary>
    /// Try all resolution strategies (Internal, then Referenced, then Referenceable).
    /// </summary>
    All
}
