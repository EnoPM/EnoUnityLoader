using System;

namespace EnoModLoader.Preloader.Patching;

/// <summary>
/// Specifies which assemblies a patcher should target.
/// </summary>
public enum PatcherTarget
{
    /// <summary>
    /// Target plugin assemblies from the mods folder.
    /// </summary>
    Plugins,

    /// <summary>
    /// Target IL2CPP interop assemblies.
    /// </summary>
    Interop,

    /// <summary>
    /// Target all assemblies (plugins and interop).
    /// </summary>
    All
}

/// <summary>
/// Specifies which type of assemblies the patcher should process.
/// </summary>
/// <remarks>
/// Apply this attribute to a patcher class to control which assemblies it processes.
/// If not specified, the patcher will target all assemblies by default.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PatcherTargetAttribute : Attribute
{
    /// <summary>
    /// The target assemblies for this patcher.
    /// </summary>
    public PatcherTarget Target { get; }

    /// <summary>
    /// Creates a new PatcherTargetAttribute.
    /// </summary>
    /// <param name="target">The target assemblies.</param>
    public PatcherTargetAttribute(PatcherTarget target)
    {
        Target = target;
    }
}
