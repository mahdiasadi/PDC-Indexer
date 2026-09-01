using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Watchers;

public static class VolumeWatcherFactory
{
    public static IVolumeWatcher Create(char driveLetter, IEnumerable<FileEntry>? existingEntries = null)
    {
        driveLetter = char.ToUpperInvariant(driveLetter);
        var fsType = FileSystemFactory.DetectFileSystemType(driveLetter);

        return fsType switch
        {
            FileSystemType.Ntfs => new UsnVolumeWatcher(driveLetter, existingEntries),
            FileSystemType.Fat32 or FileSystemType.ExFat or FileSystemType.Smb => new FatWatcher(driveLetter),
            _ => throw new NotSupportedException(
                     $"No watcher available for drive {driveLetter}: (filesystem type: {fsType})"),
        };
    }
}
