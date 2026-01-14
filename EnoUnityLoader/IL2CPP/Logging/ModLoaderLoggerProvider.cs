using System;
using System.Collections.Generic;
using EnoUnityLoader.Logging;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace EnoUnityLoader.IL2CPP.Logging;

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
            (MSLogLevelToModLoaderLogLevel(logLevel) & Logger.ListenedLogLevels) != EnoUnityLoader.Logging.LogLevel.None;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new EmptyScope();

        public void Dispose() => Logger.Sources.Remove(this);

        public required string SourceName { get; init; }

        public event EventHandler<LogEventArgs>? LogEvent;

        private static EnoUnityLoader.Logging.LogLevel MSLogLevelToModLoaderLogLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace       => EnoUnityLoader.Logging.LogLevel.Debug,
            LogLevel.Debug       => EnoUnityLoader.Logging.LogLevel.Debug,
            LogLevel.Information => EnoUnityLoader.Logging.LogLevel.Info,
            LogLevel.Warning     => EnoUnityLoader.Logging.LogLevel.Warning,
            LogLevel.Error       => EnoUnityLoader.Logging.LogLevel.Error,
            LogLevel.Critical    => EnoUnityLoader.Logging.LogLevel.Fatal,
            LogLevel.None        => EnoUnityLoader.Logging.LogLevel.None,
            _                    => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
    }
}
