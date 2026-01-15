using EnoUnityLoader.Logging;
using EnoUnityLoader.Logging.Interpolation;

namespace BetterVanilla.Core;

public static class Ls
{
    private static ManualLogSource Logger { get; set; }

    public static void SetLogSource(ManualLogSource logSource)
    {
        Logger = logSource;
    }

    public static void LogError(object data) => Logger.LogError(data);
    public static void LogError(ModLoaderErrorLogInterpolatedStringHandler logHandler) => Logger.LogError(logHandler);
    
    public static void LogWarning(object data) => Logger.LogWarning(data);
    public static void LogWarning(ModLoaderErrorLogInterpolatedStringHandler logHandler) => Logger.LogWarning(logHandler);
    
    public static void LogMessage(object data) => Logger.LogMessage(data);
    public static void LogMessage(ModLoaderErrorLogInterpolatedStringHandler logHandler) => Logger.LogMessage(logHandler);
    
    public static void LogInfo(object data) => Logger.LogInfo(data);
    public static void LogInfo(ModLoaderErrorLogInterpolatedStringHandler logHandler) => Logger.LogInfo(logHandler);
    
    public static void LogDebug(object data) => Logger.LogDebug(data);
    public static void LogDebug(ModLoaderErrorLogInterpolatedStringHandler logHandler) => Logger.LogDebug(logHandler);
}