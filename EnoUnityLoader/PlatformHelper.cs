using System;
using System.Runtime.InteropServices;

namespace EnoModLoader;

/// <summary>
/// Platform detection helper compatible with MonoMod's old API.
/// Replaces MonoMod.Utils.PlatformHelper for .NET 10 compatibility.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Gets or sets the current platform.
    /// </summary>
    public static Platform Current { get; set; }

    static PlatformHelper()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Current = Platform.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Current = Platform.Linux | Platform.Unix;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Current = Platform.MacOS | Platform.Unix;
        else
            Current = Platform.Unknown;

        // Add architecture info
        Current |= RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => Platform.X86,
            Architecture.X64 => Platform.X64 | Platform.Bits64,
            Architecture.Arm => Platform.ARM,
            Architecture.Arm64 => Platform.ARM64 | Platform.Bits64,
            _ => Platform.Unknown
        };
    }

    /// <summary>
    /// Check if the current platform matches the given flags.
    /// </summary>
    public static bool Is(Platform platform) => (Current & platform) == platform;

    /// <summary>
    /// Gets the library suffix for the current platform (dll, so, dylib) without the leading dot.
    /// </summary>
    public static string LibrarySuffix
    {
        get
        {
            if (Is(Platform.Windows))
                return "dll";
            if (Is(Platform.MacOS))
                return "dylib";
            return "so";
        }
    }
}

/// <summary>
/// Platform flags.
/// </summary>
[Flags]
public enum Platform
{
    Unknown = 0,

    // OS flags
    Windows = 1 << 0,
    Linux = 1 << 1,
    MacOS = 1 << 2,
    Unix = 1 << 3,
    Android = 1 << 4,
    iOS = 1 << 5,
    Wine = 1 << 6,

    // Architecture flags
    X86 = 1 << 16,
    X64 = 1 << 17,
    ARM = 1 << 18,
    ARM64 = 1 << 19,
    Bits64 = 1 << 20,
}
