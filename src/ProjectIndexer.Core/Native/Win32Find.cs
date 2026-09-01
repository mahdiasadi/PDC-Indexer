using System.Runtime.InteropServices;

namespace ProjectIndexer.Core.Native;

internal static class Win32Find
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFindHandle FindFirstFileW(
        string lpFileName,
        out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindNextFileW(
        SafeFindHandle hFindFile,
        out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindClose(IntPtr hFindFile);
}

internal class SafeFindHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeFindHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        return Win32Find.FindClose(handle);
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WIN32_FIND_DATAW
{
    public FileAttributes dwFileAttributes;
    public long ftCreationTime;
    public long ftLastAccessTime;
    public long ftLastWriteTime;
    public uint nFileSizeHigh;
    public uint nFileSizeLow;
    public uint dwReserved0;
    public uint dwReserved1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string cFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
    public string cAlternateFileName;

    public readonly long FileSize => ((long)nFileSizeHigh << 32) | nFileSizeLow;
    public readonly bool IsDirectory => (dwFileAttributes & FileAttributes.Directory) != 0;
    public readonly bool IsReparsePoint => (dwFileAttributes & FileAttributes.ReparsePoint) != 0;
    public readonly bool IsHidden => (dwFileAttributes & FileAttributes.Hidden) != 0;
    public readonly bool IsSystem => (dwFileAttributes & FileAttributes.System) != 0;
    public readonly bool IsReadOnly => (dwFileAttributes & FileAttributes.ReadOnly) != 0;
    public readonly bool IsArchive => (dwFileAttributes & FileAttributes.Archive) != 0;
    public readonly bool IsTemporary => (dwFileAttributes & FileAttributes.Temporary) != 0;
    public readonly bool IsJunction => IsReparsePoint && dwReserved0 == 0xA0000003;
    public readonly bool IsSymlink => IsReparsePoint && dwReserved0 == 0xA000000C;
    public readonly bool IsSpecialEntry => cFileName == "." || cFileName == "..";
    internal readonly DateTime? CreationTime => FileTimeToDateTime(ftCreationTime);
    internal readonly DateTime? LastWriteTime => FileTimeToDateTime(ftLastWriteTime);
    internal readonly DateTime? LastAccessTime => FileTimeToDateTime(ftLastAccessTime);

    private static DateTime? FileTimeToDateTime(long fileTime)
    {
        if (fileTime == 0) return null;
        try { return DateTime.FromFileTimeUtc(fileTime); }
        catch { return null; }
    }
}
