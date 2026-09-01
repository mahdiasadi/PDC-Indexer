namespace ProjectIndexer.Core.Archiving;

public class ArchiveInfo
{
    public string FilePath { get; init; } = string.Empty;
    public char DriveLetter { get; init; }
    public string DriveLabel { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public long EntryCount { get; init; }
    public long FileSize { get; init; }
    public string VolumeSerial { get; init; } = string.Empty;
    public string DisplayName => $"{DriveLetter}:\\ — {CreatedAt:yyyy-MM-dd HH:mm} ({EntryCount:N0} entries)";
}
