using EnoModLoader.Logging;

namespace EnoModLoader.Preloader;

/// <summary>
///     Static log source for preloader messages.
/// </summary>
public static class PreloaderLogger
{
    /// <summary>
    ///     The preloader log source.
    /// </summary>
    public static ManualLogSource Log { get; } = Logger.CreateLogSource("Preloader");
}
