using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Mft;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;

namespace ProjectIndexer.Core;

public class MftIndexer : IFileSystemProvider
{
    public FileSystemType FileSystemType => FileSystemType.Ntfs;
    public char DriveLetter { get; }
    public bool SupportsJournaling => true;
    public Action<FileEntry>? OnEntryIndexed { get; set; }

    public MftIndexer(char driveLetter)
    {
        DriveLetter = char.ToUpperInvariant(driveLetter);
    }

    public bool CanProcess()
    {
        return IsNtfsVolume();
    }

    public bool IsNtfsVolume()
    {
        try
        {
            var driveInfo = new DriveInfo(DriveLetter.ToString());
            return driveInfo.IsReady &&
                   driveInfo.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool CanOpenVolume()
    {
        try
        {
            using var parser = new MftParser(DriveLetter);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public List<FileEntry> EnumerateFiles(IProgress<IndexProgress>? progress = null)
    {
        using var parser = new MftParser(DriveLetter);
        parser.EntryParsed = OnEntryIndexed;
        return parser.ParseAll(progress).ToList();
    }

    public List<FileEntry> GetJournalChanges(long lastUsn, long journalId)
    {
        string volumePath = $@"\\.\{DriveLetter}:";
        using var volHandle = Win32Native.CreateFile(
            volumePath,
            Win32Native.GENERIC_READ,
            Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Win32Native.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (volHandle.IsInvalid)
            return [];

        try
        {
            var journalData = UsnJournal.QueryJournal(volHandle);
            if (journalData.UsnJournalId != journalId)
                return [];

            if (lastUsn < journalData.FirstUsn)
                lastUsn = journalData.FirstUsn;

            var changes = new List<FileEntry>();
            long currentUsn = lastUsn;

            var records = UsnJournal.ReadJournalRecords(volHandle, currentUsn, journalId);
            foreach (var record in records)
            {
                bool isCreate = (record.Reason & UsnJournal.USN_REASON_FILE_CREATE) != 0;
                bool isDelete = (record.Reason & (UsnJournal.USN_REASON_FILE_DELETE | UsnJournal.USN_REASON_EXTEND_FILE_DELETE)) != 0;

                if (isCreate || isDelete)
                {
                    changes.Add(new FileEntry
                    {
                        Frn = (ulong)record.FileReferenceNumber,
                        ParentFrn = (ulong)record.ParentFileReferenceNumber,
                        Name = record.FileName,
                        FullPath = "",
                        IsDirectory = record.IsDirectory,
                        Size = 0,
                        DriveLetter = DriveLetter,
                    });
                }
            }

            return changes;
        }
        catch
        {
            return [];
        }
    }

    public static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static IReadOnlyList<char> GetNtfsDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Name[0])
            .ToList();
    }
}
