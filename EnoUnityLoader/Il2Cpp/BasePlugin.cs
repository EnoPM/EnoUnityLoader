using EnoUnityLoader.Configuration;
using EnoUnityLoader.Contract;
using EnoUnityLoader.Logging;
using Il2CppInterop.Runtime.InteropTypes;

namespace EnoUnityLoader.Il2Cpp;

/// <summary>
///     Base class for IL2CPP plugins.
/// </summary>
public abstract class BasePlugin
{
    /// <summary>
    ///     Creates a new plugin instance.
    /// </summary>
    protected BasePlugin()
    {
        var metadata = MetadataHelper.GetMetadata(this);

        Log = Logger.CreateLogSource(metadata?.Name ?? "Unknown");

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, (metadata?.Guid ?? "unknown") + ".cfg"), false, metadata);
    }

    /// <summary>
    ///     The log source for this plugin.
    /// </summary>
    public ManualLogSource Log { get; }

    /// <summary>
    ///     The configuration file for this plugin.
    /// </summary>
    public ConfigFile Config { get; }

    /// <summary>
    ///     Called when the plugin is loaded.
    /// </summary>
    public abstract void Load();

    /// <summary>
    ///     Called when the plugin is unloaded.
    /// </summary>
    /// <returns>True if unload was successful.</returns>
    public virtual bool Unload() => false;

    /// <summary>
    ///     Add a Component (e.g. MonoBehaviour) into Unity scene.
    ///     Automatically registers the type with Il2Cpp Type system if it isn't already.
    /// </summary>
    /// <typeparam name="T">Type of the component to add.</typeparam>
    public T AddComponent<T>() where T : Il2CppObjectBase => IL2CPPChainLoader.AddUnityComponent<T>();
}
