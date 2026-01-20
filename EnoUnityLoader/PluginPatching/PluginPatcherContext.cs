using System.Collections.Generic;

namespace EnoUnityLoader.PluginPatching;

/// <summary>
/// Context shared among plugin patchers during the patching process.
/// </summary>
public class PluginPatcherContext
{
    /// <summary>
    /// All discovered and loaded plugin patchers.
    /// </summary>
    public List<BasePluginPatcher> Patchers { get; } = [];

    /// <summary>
    /// Path to the cache directory for patched plugins.
    /// </summary>
    public string CachePath { get; internal set; } = string.Empty;

    /// <summary>
    /// Path to the cache index file.
    /// </summary>
    public string CacheIndexPath { get; internal set; } = string.Empty;

    /// <summary>
    /// The file path of the plugin currently being patched.
    /// Set by the engine before calling Patch() on each patcher.
    /// </summary>
    public string CurrentPluginLocation { get; internal set; } = string.Empty;
}
