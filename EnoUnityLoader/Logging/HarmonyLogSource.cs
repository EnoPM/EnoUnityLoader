using System;
using System.Collections.Generic;
using EnoModLoader.Configuration;
using HarmonyLogger = HarmonyLib.Tools.Logger;

namespace EnoModLoader.Logging;

/// <summary>
///     A log source that listens to HarmonyX log messages.
/// </summary>
public class HarmonyLogSource : ILogSource
{
    private static readonly ConfigEntry<HarmonyLogger.LogChannel> LogChannels = ConfigFile.CoreConfig.Bind(
     "Harmony.Logger",
     "LogChannels",
     HarmonyLogger.LogChannel.Warn | HarmonyLogger.LogChannel.Error,
     "Specifies which Harmony log channels to listen to.\nNOTE: IL channel dumps the whole patch methods, use only when needed!");

    private static readonly Dictionary<HarmonyLogger.LogChannel, LogLevel> LevelMap = new()
    {
        [HarmonyLogger.LogChannel.Info] = LogLevel.Info,
        [HarmonyLogger.LogChannel.Warn] = LogLevel.Warning,
        [HarmonyLogger.LogChannel.Error] = LogLevel.Error,
        [HarmonyLogger.LogChannel.IL] = LogLevel.Debug
    };

    /// <summary>
    ///     Creates a new HarmonyX log source.
    /// </summary>
    public HarmonyLogSource()
    {
        HarmonyLogger.ChannelFilter = LogChannels.Value;
        HarmonyLogger.MessageReceived += HandleHarmonyMessage;
    }

    /// <inheritdoc />
    public void Dispose() => HarmonyLogger.MessageReceived -= HandleHarmonyMessage;

    /// <inheritdoc />
    public string SourceName { get; } = "HarmonyX";

    /// <inheritdoc />
    public event EventHandler<LogEventArgs>? LogEvent;

    private void HandleHarmonyMessage(object? sender, HarmonyLogger.LogEventArgs e)
    {
        if (!LevelMap.TryGetValue(e.LogChannel, out var level))
            return;

        LogEvent?.Invoke(this, new LogEventArgs(e.Message, level, this));
    }
}
