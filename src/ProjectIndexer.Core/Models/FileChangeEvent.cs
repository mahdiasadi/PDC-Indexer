namespace ProjectIndexer.Core.Models;

public enum ChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}

public class FileChangeEvent
{
    public ChangeType Type { get; init; }
    public string FullPath { get; init; } = string.Empty;
    public string? OldPath { get; init; }
    public char DriveLetter { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public ulong Frn { get; init; }
}
