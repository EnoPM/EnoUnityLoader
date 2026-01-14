using System;
using System.Collections.Generic;
using EnoModLoader.Logging;
using Microsoft.Extensions.Logging;
using ModLoaderLogLevel = EnoModLoader.Logging.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace EnoModLoader.IL2CPP.Logging;

internal class ModLoaderLoggerProvider : ILoggerProvider
{
    private readonly List<ModLoaderLogger> loggers = [];

    public void Dispose()
    {
        foreach (var modLoaderLogger in loggers)
            modLoaderLogger.Dispose();
        loggers.Clear();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new ModLoaderLogger { SourceName = categoryName };
        Logger.Sources.Add(logger);
        loggers.Add(logger);
        return logger;
    }

    private sealed class EmptyScope : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class ModLoaderLogger : ILogSource, ILogger
    {
        public void Log<TState>(LogLevel logLevel,
                                EventId eventId,
                                TState state,
                                Exception? exception,
                                Func<TState, Exception?, string> formatter)
        {
            var logLine = state?.ToString() ?? string.Empty;

            if (exception != null)
                logLine += $"\nException: {exception}";

            LogEvent?.Invoke(this,
                             new LogEventArgs(logLine, MSLogLevelToModLoaderLogLevel(logLevel),
                                              this));
        }


        public bool IsEnabled(LogLevel logLevel) =>
            (MSLogLevelToModLoaderLogLevel(logLevel) & Logger.ListenedLogLevels) != EnoModLoader.Logging.LogLevel.None;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new EmptyScope();

        public void Dispose() => Logger.Sources.Remove(this);

        public required string SourceName { get; init; }

        public event EventHandler<LogEventArgs>? LogEvent;

        private static EnoModLoader.Logging.LogLevel MSLogLevelToModLoaderLogLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => EnoModLoader.Logging.LogLevel.Debug,
            LogLevel.Debug       => EnoModLoader.Logging.LogLevel.Debug,
            LogLevel.Information => EnoModLoader.Logging.LogLevel.Info,
            LogLevel.Warning     => EnoModLoader.Logging.LogLevel.Warning,
            LogLevel.Error       => EnoModLoader.Logging.LogLevel.Error,
            LogLevel.Critical    => EnoModLoader.Logging.LogLevel.Fatal,
            LogLevel.None        => EnoModLoader.Logging.LogLevel.None,
            _                    => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
    }
}
