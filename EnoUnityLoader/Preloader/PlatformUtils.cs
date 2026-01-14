using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EnoModLoader.Preloader;

public static class PlatformUtils
{
    public static readonly bool ProcessIs64Bit = IntPtr.Size >= 8;
    public static Version? WindowsVersion { get; set; }
    public static string? WineVersion { get; set; }

    public static string? LinuxArchitecture { get; set; }
    public static string? LinuxKernelVersion { get; set; }

    [DllImport("libc.so.6", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl,
               CharSet = CharSet.Ansi)]
    private static extern IntPtr uname_linux(ref utsname_linux utsname);

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl,
               CharSet = CharSet.Ansi)]
    private static extern IntPtr uname_osx(ref utsname_osx utsname);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern bool RtlGetVersion(ref WindowsOSVersionInfoExW versionInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string libraryName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    private static bool Is(this Platform current, Platform expected) => (current & expected) == expected;

    /// <summary>
    ///     Recreation of MonoMod's PlatformHelper.DeterminePlatform method, but with libc calls instead of creating processes.
    /// </summary>
    public static void SetPlatform()
    {
        var current = Platform.Unknown;

        // For old Mono, get from a private property to accurately get the platform.
        // static extern PlatformID Platform
        var p_Platform = typeof(Environment).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static);
        string platID;
        if (p_Platform != null)
            platID = p_Platform.GetValue(null, [])?.ToString() ?? string.Empty;
        else
            // For .NET and newer Mono, use the usual value.
            platID = Environment.OSVersion.Platform.ToString();
        platID = platID.ToLowerInvariant();

        if (platID.Contains("win"))
            current = Platform.Windows;
        else if (platID.Contains("mac") || platID.Contains("osx"))
            current = Platform.MacOS;
        else if (platID.Contains("lin") || platID.Contains("unix"))
            current = Platform.Linux;

        if (current.Is(Platform.Linux) && Directory.Exists("/data") && File.Exists("/system/build.prop"))
            current = Platform.Android;
        else if (current.Is(Platform.Unix) && Directory.Exists("/System/Library/AccessibilityBundles"))
            current = Platform.iOS;

        if (current.Is(Platform.Windows))
        {
            var windowsVersionInfo = new WindowsOSVersionInfoExW();
            RtlGetVersion(ref windowsVersionInfo);

            WindowsVersion = new Version((int)windowsVersionInfo.dwMajorVersion,
                                         (int)windowsVersionInfo.dwMinorVersion, 0,
                                         (int)windowsVersionInfo.dwBuildNumber);

            var ntDll = LoadLibrary("ntdll.dll");
            if (ntDll != IntPtr.Zero)
            {
                var wineGetVersion = GetProcAddress(ntDll, "wine_get_version");
                if (wineGetVersion != IntPtr.Zero)
                {
                    current |= Platform.Wine;
                    var getVersion = Marshal.GetDelegateForFunctionPointer<GetWineVersionDelegate>(wineGetVersion);
                    WineVersion = getVersion();
                }
            }
        }

        // Is64BitOperatingSystem is available in .NET 10
        current |= Environment.Is64BitOperatingSystem ? Platform.Bits64 : 0;

        if (current.Is(Platform.MacOS) || current.Is(Platform.Linux))
        {
            string? arch = null;
            IntPtr result;

            if (current.Is(Platform.MacOS))
            {
                var utsname_osx = new utsname_osx();
                result = PlatformUtils.uname_osx(ref utsname_osx);
                arch = utsname_osx.machine;
            }
            else
            {
                // Linux
                var utsname_linux = new utsname_linux();
                result = PlatformUtils.uname_linux(ref utsname_linux);
                arch = utsname_linux.machine;

                LinuxArchitecture = utsname_linux.machine;
                LinuxKernelVersion = utsname_linux.version;
            }

            if (result == IntPtr.Zero && arch != null && (arch.StartsWith("aarch") || arch.StartsWith("arm")))
                current |= Platform.ARM;
        }
        else
        {
            // Detect ARM based on RuntimeInformation (available in .NET 10)
            var arch = RuntimeInformation.ProcessArchitecture;
            if (arch == Architecture.Arm || arch == Architecture.Arm64)
                current |= Platform.ARM;
        }

        PlatformHelper.Current = current;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPStr)]
    private delegate string GetWineVersionDelegate();

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct WindowsOSVersionInfoExW
    {
        public uint dwOSVersionInfoSize;
        public uint dwMajorVersion;
        public uint dwMinorVersion;
        public uint dwBuildNumber;
        public uint dwPlatformId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? szCSDVersion;

        public ushort wServicePackMajor;
        public ushort wServicePackMinor;
        public ushort wSuiteMask;
        public byte wProductType;
        public byte wReserved;

        public WindowsOSVersionInfoExW()
        {
            dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(WindowsOSVersionInfoExW));
            dwMajorVersion = 0;
            dwMinorVersion = 0;
            dwBuildNumber = 0;
            dwPlatformId = 0;
            szCSDVersion = null;
            wServicePackMajor = 0;
            wServicePackMinor = 0;
            wSuiteMask = 0;
            wProductType = 0;
            wReserved = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct utsname_osx
    {
        private const int osx_utslen = 256;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
        public string? sysname;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
        public string? nodename;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
        public string? release;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
        public string? version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
        public string? machine;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct utsname_linux
    {
        private const int linux_utslen = 65;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? sysname;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? nodename;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? release;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? machine;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string? domainname;
    }
}
