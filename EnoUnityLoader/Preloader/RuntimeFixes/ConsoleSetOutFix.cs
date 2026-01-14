using System.IO;
using System.Text;
using EnoModLoader.Logging;
using HarmonyLib;

namespace EnoModLoader.Preloader.RuntimeFixes;

/// <summary>
///     Fixes Console.SetOut to redirect to ModLoader logging.
/// </summary>
public static class ConsoleSetOutFix
{
    private static LoggedTextWriter? loggedTextWriter;
    internal static ManualLogSource ConsoleLogSource = Logger.CreateLogSource("Console");

    /// <summary>
    ///     Applies the Console.SetOut fix.
    /// </summary>
    public static void Apply()
    {
        loggedTextWriter = new LoggedTextWriter { Parent = System.Console.Out };
        System.Console.SetOut(loggedTextWriter);

        // TODO: Harmony patching disabled for .NET 10 compatibility
        // MonoMod.RuntimeDetour 25.2+ required but not available on public NuGet
        // Harmony.CreateAndPatchAll(typeof(ConsoleSetOutFix));
    }

    [HarmonyPatch(typeof(System.Console), nameof(System.Console.SetOut))]
    [HarmonyPrefix]
    private static bool OnSetOut(TextWriter newOut)
    {
        if (loggedTextWriter != null)
            loggedTextWriter.Parent = newOut;
        return false;
    }
}

internal class LoggedTextWriter : TextWriter
{
    public override Encoding Encoding { get; } = Encoding.UTF8;

    public TextWriter? Parent { get; set; }

    public override void Flush() => Parent?.Flush();

    public override void Write(string? value)
    {
        if (value != null)
        {
            ConsoleSetOutFix.ConsoleLogSource.Log(LogLevel.Info, value);
            Parent?.Write(value);
        }
    }

    public override void WriteLine(string? value)
    {
        if (value != null)
        {
            ConsoleSetOutFix.ConsoleLogSource.Log(LogLevel.Info, value);
            Parent?.WriteLine(value);
        }
    }
}
