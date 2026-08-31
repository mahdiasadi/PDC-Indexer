namespace ProjectIndexer.Core.Models;

public class FileEntry
{
    public ulong Frn { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ulong ParentFrn { get; set; }
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? LastModifiedTime { get; set; }
    public DateTime? LastAccessTime { get; set; }
    public DateTime? MftModifiedTime { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsHidden { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsSystem { get; set; }
    public bool IsArchive { get; set; }
    public bool IsTemporary { get; set; }
    public char DriveLetter { get; set; }
    public string NameExtension => string.IsNullOrEmpty(Name) ? "" :
        Name.Contains('.') ? Name[(Name.LastIndexOf('.') + 1)..].ToLowerInvariant() : "";

    public override string ToString() => FullPath;
}
