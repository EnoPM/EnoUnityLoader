using System.Text.Json.Serialization;

namespace EnoUnityLoader.Ipc.Messages;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(IpcMessage))]
[JsonSerializable(typeof(ProgressMessage))]
[JsonSerializable(typeof(StatusMessage))]
[JsonSerializable(typeof(LogMessage))]
[JsonSerializable(typeof(ModListMessage))]
[JsonSerializable(typeof(ModActionRequest))]
[JsonSerializable(typeof(ShutdownMessage))]
[JsonSerializable(typeof(ReadyMessage))]
[JsonSerializable(typeof(ModInfo))]
[JsonSerializable(typeof(List<ModInfo>))]
internal partial class JsonContext : JsonSerializerContext
{
}
