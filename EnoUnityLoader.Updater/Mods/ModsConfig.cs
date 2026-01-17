using YamlDotNet.Serialization;

namespace EnoUnityLoader.Updater.Mods;

/// <summary>
/// Configuration for mods to install/update from GitHub.
/// </summary>
internal sealed class ModsConfig
{
    [YamlMember(Alias = "mods")]
    public List<ModEntry> Mods { get; set; } = [];
}

/// <summary>
/// Configuration for a single mod.
/// </summary>
internal sealed class ModEntry
{
    /// <summary>
    /// Display name of the mod (also used as folder name).
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// GitHub repository in "owner/repo" format.
    /// </summary>
    [YamlMember(Alias = "repo")]
    public string Repo { get; set; } = "";

    /// <summary>
    /// Main assembly filename used to check the installed version.
    /// </summary>
    [YamlMember(Alias = "mainAssembly")]
    public string MainAssembly { get; set; } = "";

    /// <summary>
    /// Whether this mod should be installed/updated.
    /// </summary>
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = true;
}
