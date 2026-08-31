using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProjectIndexer.Core.Native;

internal static class UsnJournal
{
    internal const uint FSCTL_QUERY_USN_JOURNAL = 0x00090094;
    internal const uint FSCTL_CREATE_USN_JOURNAL = 0x000900E7;
    internal const uint FSCTL_READ_USN_JOURNAL = 0x000900BB;

    internal const uint USN_REASON_FILE_CREATE = 0x00000100;
    internal const uint USN_REASON_FILE_DELETE = 0x00000200;
    internal const uint USN_REASON_EXTEND_FILE_DELETE = 0x00000400;
    internal const uint USN_REASON_RENAME_OLD_NAME = 0x00001000;
    internal const uint USN_REASON_RENAME_NEW_NAME = 0x00002000;
    internal const uint USN_REASON_DATA_OVERWRITE = 0x00000001;
    internal const uint USN_REASON_DATA_EXTEND = 0x00000002;
    internal const uint USN_REASON_DATA_TRUNCATION = 0x00000004;
    internal const uint USN_REASON_CLOSE = 0x80000000;
    internal const uint USN_REASON_HARD_LINK_CHANGE = 0x00010000;

    internal const uint ALL_CHANGE_REASONS =
        USN_REASON_FILE_CREATE | USN_REASON_FILE_DELETE |
        USN_REASON_EXTEND_FILE_DELETE | USN_REASON_RENAME_OLD_NAME |
        USN_REASON_RENAME_NEW_NAME | USN_REASON_DATA_OVERWRITE |
        USN_REASON_DATA_EXTEND | USN_REASON_DATA_TRUNCATION |
        USN_REASON_CLOSE | USN_REASON_HARD_LINK_CHANGE;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        uint nInBufferSize,
        [Out] byte[]? lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    internal static UsnJournalData QueryJournal(SafeFileHandle volumeHandle)
    {
        var outBuf = new byte[64];
        if (!DeviceIoControl(volumeHandle, FSCTL_QUERY_USN_JOURNAL,
                null, 0, outBuf, (uint)outBuf.Length, out uint returned, IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"FSCTL_QUERY_USN_JOURNAL failed: error {err}");
        }

        return UsnJournalData.Parse(outBuf);
    }

    internal static void CreateJournal(SafeFileHandle volumeHandle, ulong maxSize = 0x20000000, ulong allocDelta = 0x100000)
    {
        var inBuf = new byte[16];
        BitConverter.GetBytes(maxSize).CopyTo(inBuf, 0);
        BitConverter.GetBytes(allocDelta).CopyTo(inBuf, 8);

        if (!DeviceIoControl(volumeHandle, FSCTL_CREATE_USN_JOURNAL,
                inBuf, (uint)inBuf.Length, null, 0, out _, IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            if (err != 0x57)
                throw new InvalidOperationException($"FSCTL_CREATE_USN_JOURNAL failed: error {err}");
        }
    }

    internal static List<UsnRecord> ReadJournalRecords(SafeFileHandle volumeHandle, long startUsn, long usnJournalId)
    {
        var readData = new byte[32];
        BitConverter.GetBytes(startUsn).CopyTo(readData, 0);
        BitConverter.GetBytes(ALL_CHANGE_REASONS).CopyTo(readData, 8);
        BitConverter.GetBytes(0u).CopyTo(readData, 12);
        BitConverter.GetBytes(0ul).CopyTo(readData, 16);
        BitConverter.GetBytes(0ul).CopyTo(readData, 24);
        BitConverter.GetBytes(usnJournalId).CopyTo(readData, 32);
        BitConverter.GetBytes((ushort)2).CopyTo(readData, 40);
        BitConverter.GetBytes((ushort)3).CopyTo(readData, 42);

        var outBuf = new byte[4 * 1024 * 1024];
        if (!DeviceIoControl(volumeHandle, FSCTL_READ_USN_JOURNAL,
                readData, (uint)readData.Length, outBuf, (uint)outBuf.Length,
                out uint returned, IntPtr.Zero))
        {
            int err = Marshal.GetLastWin32Error();
            if (err == 0x6B || err == 0x1F)
                return [];
            throw new InvalidOperationException($"FSCTL_READ_USN_JOURNAL failed: error {err}");
        }

        if (returned < 4) return [];

        uint totalRecordLength = BitConverter.ToUInt32(outBuf, 0);
        if (totalRecordLength == 0 || totalRecordLength + 4 > returned) return [];

        var records = new List<UsnRecord>();
        int offset = 4;
        int end = 4 + (int)totalRecordLength;

        while (offset + 4 <= end)
        {
            int recordLen = BitConverter.ToInt32(outBuf, offset);
            if (recordLen <= 0 || offset + recordLen > end) break;

            var record = UsnRecord.Parse(outBuf, offset);
            records.Add(record);
            offset += recordLen;
        }

        return records;
    }
}

internal struct UsnJournalData
{
    public long UsnJournalId;
    public long FirstUsn;
    public long NextUsn;
    public long LowestValidUsn;
    public long MaxUsn;

    public static UsnJournalData Parse(byte[] data)
    {
        return new UsnJournalData
        {
            UsnJournalId = BitConverter.ToInt64(data, 0),
            FirstUsn = BitConverter.ToInt64(data, 8),
            NextUsn = BitConverter.ToInt64(data, 16),
            LowestValidUsn = BitConverter.ToInt64(data, 24),
            MaxUsn = BitConverter.ToInt64(data, 32),
        };
    }
}

internal struct UsnRecord
{
    public int RecordLength;
    public ushort MajorVersion;
    public long FileReferenceNumber;
    public long ParentFileReferenceNumber;
    public long Usn;
    public long TimeStamp;
    public uint Reason;
    public uint FileAttributes;
    public ushort FileNameLength;
    public ushort FileNameOffset;
    public string FileName;

    public readonly bool IsDirectory => (FileAttributes & 0x10) != 0;
    public readonly DateTime TimeUtc => DateTime.FromFileTimeUtc(TimeStamp);

    public static UsnRecord Parse(byte[] buffer, int offset)
    {
        int recordLength = BitConverter.ToInt32(buffer, offset);
        ushort majorVer = BitConverter.ToUInt16(buffer, offset + 4);
        long frn = BitConverter.ToInt64(buffer, offset + 8);
        long parentFrn = BitConverter.ToInt64(buffer, offset + 16);
        long usn = BitConverter.ToInt64(buffer, offset + 24);
        long timestamp = BitConverter.ToInt64(buffer, offset + 32);
        uint reason = BitConverter.ToUInt32(buffer, offset + 40);
        uint fileAttrs = BitConverter.ToUInt32(buffer, offset + 56);
        ushort fileNameLen = BitConverter.ToUInt16(buffer, offset + 60);
        ushort fileNameOff = BitConverter.ToUInt16(buffer, offset + 62);

        string name = "";
        if (fileNameLen > 0 && offset + fileNameOff + fileNameLen <= buffer.Length)
        {
            name = System.Text.Encoding.Unicode.GetString(
                buffer, offset + fileNameOff, fileNameLen);
        }

        return new UsnRecord
        {
            RecordLength = recordLength,
            MajorVersion = majorVer,
            FileReferenceNumber = frn,
            ParentFileReferenceNumber = parentFrn,
            Usn = usn,
            TimeStamp = timestamp,
            Reason = reason,
            FileAttributes = fileAttrs,
            FileNameLength = fileNameLen,
            FileNameOffset = fileNameOff,
            FileName = name,
        };
    }
}
