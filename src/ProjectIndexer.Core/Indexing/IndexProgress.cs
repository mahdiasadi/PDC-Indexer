namespace ProjectIndexer.Core.Indexing;

public class IndexProgress
{
    public long TotalRecords { get; set; }
    public long ParsedRecords { get; set; }
    public long FilesFound { get; set; }
    public long DirectoriesFound { get; set; }
    public string? CurrentPath { get; set; }
    public string DriveLetter { get; set; } = string.Empty;
    public IndexStage Stage { get; set; } = IndexStage.ReadingBootSector;
    public double PercentComplete => TotalRecords > 0 ? (double)ParsedRecords / TotalRecords * 100.0 : 0;

    public override string ToString() =>
        $"[{DriveLetter}] {Stage}: {ParsedRecords}/{TotalRecords} records, {FilesFound} files, {DirectoriesFound} dirs ({PercentComplete:F1}%)";
}

public enum IndexStage
{
    Starting,
    IncrementalUpdate,
    ReadingBootSector,
    ReadingMft,
    ParsingRecords,
    ReconstructingPaths,
    EnumeratingDirectories,
    Completed,
    Failed
}
