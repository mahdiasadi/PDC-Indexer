using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProjectIndexer.Core.Native;

internal static class Win32Native
{
    internal const uint GENERIC_READ = 0x80000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint OPEN_EXISTING = 3;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    internal const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    internal const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadFile(
        SafeFileHandle hFile,
        [Out] byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    internal const uint NO_ERROR = 0;
    internal const uint ERROR_ACCESS_DENIED = 5;
    internal const uint ERROR_SHARING_VIOLATION = 32;
    internal const uint FILE_BEGIN = 0;
}
