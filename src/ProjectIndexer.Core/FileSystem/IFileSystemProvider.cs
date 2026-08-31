using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.FileSystem;

public enum FileSystemType
{
    Ntfs,
    Fat32,
    ExFat,
    Smb,
    Unknown
}

public interface IFileSystemProvider
{
    FileSystemType FileSystemType { get; }
    char DriveLetter { get; }

    bool CanProcess();

    bool SupportsJournaling { get; }

    Action<FileEntry>? OnEntryIndexed { get; set; }

    List<FileEntry> EnumerateFiles(IProgress<IndexProgress>? progress = null);

    IndexProgress CreateProgress() => new()
    {
        DriveLetter = DriveLetter.ToString(),
        Stage = IndexStage.Starting,
    };
}
