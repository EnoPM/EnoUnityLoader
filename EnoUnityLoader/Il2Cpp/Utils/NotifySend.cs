using System;
using System.Diagnostics;
using System.IO;

namespace EnoUnityLoader.Il2Cpp.Utils;

internal static class NotifySend
{
    private const string ExecutableName = "notify-send";

    public static bool IsSupported => Find(ExecutableName) != null;

    private static string? Find(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var paths = Environment.GetEnvironmentVariable("PATH");
        if (paths == null)
            return null;

        foreach (var path in paths.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public static void Send(string summary, string body)
    {
        if (!IsSupported) throw new NotSupportedException();

        var processStartInfo = new ProcessStartInfo(Find(ExecutableName)!)
        {
            ArgumentList =
            {
                summary,
                body,
                "--app-name=ModLoader",
            },
        };

        Process.Start(processStartInfo);
    }
}
