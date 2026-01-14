using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EnoUnityLoader.Console.Unix;

internal static class UnixStreamHelper
{
    private static nint libcHandle;

    public delegate int dupDelegate(int fd);
    public delegate int fcloseDelegate(nint stream);
    public delegate nint fdopenDelegate(int fd, string mode);
    public delegate int fflushDelegate(nint stream);
    public delegate nint freadDelegate(nint ptr, nint size, nint nmemb, nint stream);
    public delegate int fwriteDelegate(nint ptr, nint size, nint nmemb, nint stream);
    public delegate int isattyDelegate(int fd);

    public static dupDelegate dup = null!;
    public static fdopenDelegate fdopen = null!;
    public static freadDelegate fread = null!;
    public static fwriteDelegate fwrite = null!;
    public static fcloseDelegate fclose = null!;
    public static fflushDelegate fflush = null!;
    public static isattyDelegate isatty = null!;

    static UnixStreamHelper()
    {
        // Try to load libc from various paths
        string[] libcPaths = [
            "libc.so.6",                // Ubuntu glibc
            "libc",                     // Linux glibc
            "/usr/lib/libSystem.dylib"  // OSX POSIX
        ];

        foreach (var path in libcPaths)
        {
            if (NativeLibrary.TryLoad(path, out libcHandle))
                break;
        }

        if (libcHandle == 0)
            throw new DllNotFoundException("Could not load libc");

        // Resolve function pointers
        dup = Marshal.GetDelegateForFunctionPointer<dupDelegate>(
            NativeLibrary.GetExport(libcHandle, "dup"));
        fdopen = Marshal.GetDelegateForFunctionPointer<fdopenDelegate>(
            NativeLibrary.GetExport(libcHandle, "fdopen"));
        fread = Marshal.GetDelegateForFunctionPointer<freadDelegate>(
            NativeLibrary.GetExport(libcHandle, "fread"));
        fwrite = Marshal.GetDelegateForFunctionPointer<fwriteDelegate>(
            NativeLibrary.GetExport(libcHandle, "fwrite"));
        fclose = Marshal.GetDelegateForFunctionPointer<fcloseDelegate>(
            NativeLibrary.GetExport(libcHandle, "fclose"));
        fflush = Marshal.GetDelegateForFunctionPointer<fflushDelegate>(
            NativeLibrary.GetExport(libcHandle, "fflush"));
        isatty = Marshal.GetDelegateForFunctionPointer<isattyDelegate>(
            NativeLibrary.GetExport(libcHandle, "isatty"));
    }

    public static Stream CreateDuplicateStream(int fileDescriptor)
    {
        var newFd = dup(fileDescriptor);
        return new UnixStream(newFd, FileAccess.Write);
    }
}
