using System;
using EnoModLoader.Configuration;
using EnoModLoader.Console;

namespace EnoModLoader.Logging;

/// <summary>
///     Logs entries using a console spawned by ModLoader.
/// </summary>
public class ConsoleLogListener : ILogListener
{
    /// <summary>
    ///     Configuration entry for the console displayed log level.
    /// </summary>
    protected static readonly ConfigEntry<LogLevel> ConfigConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
     "Logging.Console", "LogLevels",
     LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
     "Only displays the specified log levels in the console output.");

    /// <inheritdoc />
    public LogLevel LogLevelFilter => ConfigConsoleDisplayedLevel.Value;

    /// <inheritdoc />
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        ConsoleManager.SetConsoleColor(eventArgs.Level.GetConsoleColor());
        ConsoleManager.ConsoleStream?.Write(eventArgs.ToStringLine());
        ConsoleManager.SetConsoleColor(ConsoleColor.Gray);
    }

    /// <inheritdoc />
    public void Dispose() { }
}
