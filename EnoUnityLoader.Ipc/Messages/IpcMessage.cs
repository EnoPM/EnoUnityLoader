using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnoUnityLoader.Ipc.Messages;

/// <summary>
/// Base class for all IPC messages.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ProgressMessage), "progress")]
[JsonDerivedType(typeof(StatusMessage), "status")]
[JsonDerivedType(typeof(LogMessage), "log")]
[JsonDerivedType(typeof(ModListMessage), "modList")]
[JsonDerivedType(typeof(ModActionRequest), "modAction")]
[JsonDerivedType(typeof(ShutdownMessage), "shutdown")]
[JsonDerivedType(typeof(ReadyMessage), "ready")]
public abstract class IpcMessage
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonContext.Default.IpcMessage);
    }

    public static IpcMessage? Deserialize(string json)
    {
        return JsonSerializer.Deserialize(json, JsonContext.Default.IpcMessage);
    }
}

/// <summary>
/// Progress update during loading/generation.
/// </summary>
public sealed class ProgressMessage : IpcMessage
{
    public required string Stage { get; init; }
    public required string Description { get; init; }
    public double Progress { get; init; } // 0.0 to 1.0, -1 for indeterminate
    public int? CurrentItem { get; init; }
    public int? TotalItems { get; init; }
}

/// <summary>
/// Overall loader status.
/// </summary>
public sealed class StatusMessage : IpcMessage
{
    public required LoaderStatus Status { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Log message from the loader.
/// </summary>
public sealed class LogMessage : IpcMessage
{
    public required LogLevel Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// List of available/installed mods.
/// </summary>
public sealed class ModListMessage : IpcMessage
{
    public required List<ModInfo> Mods { get; init; }
}

/// <summary>
/// Request from UI to perform an action on a mod.
/// </summary>
public sealed class ModActionRequest : IpcMessage
{
    public required string ModId { get; init; }
    public required ModAction Action { get; init; }
    public string? TargetVersion { get; init; }
}

/// <summary>
/// Request to shutdown the loader/game.
/// </summary>
public sealed class ShutdownMessage : IpcMessage
{
}

/// <summary>
/// Loader is ready, game can continue.
/// </summary>
public sealed class ReadyMessage : IpcMessage
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum LoaderStatus
{
    Initializing,
    DownloadingLibraries,
    GeneratingInterop,
    LoadingMods,
    Ready,
    Error
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public enum ModAction
{
    Install,
    Update,
    Uninstall,
    Enable,
    Disable
}

public sealed class ModInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public required string Version { get; init; }
    public string? LatestVersion { get; init; }
    public bool IsInstalled { get; init; }
    public bool IsEnabled { get; init; }
    public bool HasUpdate => LatestVersion != null && LatestVersion != Version;
}
