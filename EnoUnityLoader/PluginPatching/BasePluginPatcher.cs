using EnoUnityLoader.Attributes;
using EnoUnityLoader.Configuration;
using EnoUnityLoader.Logging;
using Mono.Cecil;

namespace EnoUnityLoader.PluginPatching;

/// <summary>
/// Base class for plugin patchers that can modify plugin assemblies before loading.
/// </summary>
public abstract class BasePluginPatcher
{
    /// <summary>
    /// Creates a new plugin patcher instance.
    /// </summary>
    protected BasePluginPatcher()
    {
        Info = PluginPatcherInfoAttribute.FromType(GetType())!;

        Log = Logger.CreateLogSource(Info.Name);

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, Info.GUID + ".cfg"), false,
                                new ModInfosAttribute(Info.GUID, Info.Name, Info.Version?.ToString() ?? "0.0.0"));
    }

    /// <summary>
    /// A <see cref="ILogSource"/> instance created for use by this plugin patcher.
    /// </summary>
    public ManualLogSource Log { get; }

    /// <summary>
    /// A configuration file binding created with the <see cref="PluginPatcherInfoAttribute.GUID"/> of this patcher.
    /// </summary>
    public ConfigFile Config { get; }

    /// <summary>
    /// Metadata associated with this plugin patcher.
    /// </summary>
    public PluginPatcherInfoAttribute Info { get; }

    /// <summary>
    /// The context of the <see cref="PluginPatcherEngine"/> this patcher is associated with.
    /// </summary>
    public PluginPatcherContext? Context { get; internal set; }

    /// <summary>
    /// The path to the assembly file containing this patcher.
    /// Used for cache hash computation.
    /// </summary>
    internal string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Determines whether this patcher should process the given plugin.
    /// By default, targets ALL plugins. Override to filter specific plugins.
    /// </summary>
    /// <param name="modInfo">Metadata about the plugin being loaded (GUID, Name, Version).</param>
    /// <param name="assembly">The Cecil AssemblyDefinition of the plugin for inspection.</param>
    /// <returns>True if this patcher should process the plugin.</returns>
    public virtual bool ShouldPatch(ModInfosAttribute modInfo, AssemblyDefinition assembly) => true;

    /// <summary>
    /// Patches the plugin assembly using Mono.Cecil.
    /// Called only if <see cref="ShouldPatch"/> returns true.
    /// </summary>
    /// <param name="modInfo">Metadata about the plugin being patched (GUID, Name, Version).</param>
    /// <param name="assembly">The Cecil AssemblyDefinition to modify in-place.</param>
    public abstract void Patch(ModInfosAttribute modInfo, AssemblyDefinition assembly);

    /// <summary>
    /// Executed before any patches from this patcher are applied.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Executed after all patches from this patcher have been applied.
    /// </summary>
    public virtual void Finalizer() { }
}
