using Avalonia;

namespace EnoUnityLoader.Ui;

internal static class Program
{
    /// <summary>
    /// The game process ID to monitor. When this process exits, the UI will close.
    /// </summary>
    public static int? GameProcessId { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ParseArguments(args);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void ParseArguments(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--game-pid" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var pid))
                {
                    GameProcessId = pid;
                }
            }
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
