using ProjectIndexer.Core;
using ProjectIndexer.Core.Indexing;

namespace ProjectIndexer.Core.Tests;

public class MftParserTests
{
    [Fact]
    public void MftIndexer_Constructor_UpperCasesDriveLetter()
    {
        var indexer = new MftIndexer('c');
        Assert.Equal('C', indexer.DriveLetter);
    }

    [Fact]
    public void MftIndexer_Constructor_KeepsUpperCase()
    {
        var indexer = new MftIndexer('D');
        Assert.Equal('D', indexer.DriveLetter);
    }

    [Fact]
    public void IsAdministrator_ReturnsBool()
    {
        bool result = MftIndexer.IsAdministrator();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetNtfsDrives_ReturnsList()
    {
        var drives = MftIndexer.GetNtfsDrives();
        Assert.NotNull(drives);
        Assert.IsAssignableFrom<IReadOnlyList<char>>(drives);
    }

    [Fact]
    public void IsNtfsVolume_OnInvalidDrive_ReturnsFalse()
    {
        var indexer = new MftIndexer('Z');
        bool result = indexer.IsNtfsVolume();
        Assert.False(result);
    }
}

public class MftIndexerIntegrationTests
{
    private static bool IsAdmin => MftIndexer.IsAdministrator();
    private static IReadOnlyList<char>? _drives;
    private static IReadOnlyList<char> Drives =>
        _drives ??= MftIndexer.GetNtfsDrives();

    [Fact]
    public void EnumerateFiles_OnSystemDrive_ReturnsEntries()
    {
        if (!IsAdmin || Drives.Count == 0) return;

        var indexer = new MftIndexer(Drives[0]);
        var entries = indexer.EnumerateFiles();

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Name.Contains("Windows", StringComparison.OrdinalIgnoreCase)
                                   || e.Name.Contains("Program Files", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnumerateFiles_ReportsProgress()
    {
        if (!IsAdmin || Drives.Count == 0) return;

        var indexer = new MftIndexer(Drives[0]);
        var progress = new TestProgress();
        var entries = indexer.EnumerateFiles(progress);

        Assert.True(progress.Stages.Count > 0);
        Assert.Equal(IndexStage.Completed, progress.Stages.Last());
    }

    [Fact]
    public void EnumerateFiles_ParsingSpeed_Reasonable()
    {
        if (!IsAdmin || Drives.Count == 0) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var indexer = new MftIndexer(Drives[0]);
        var entries = indexer.EnumerateFiles();
        sw.Stop();

        double entriesPerSecond = entries.Count / sw.Elapsed.TotalSeconds;
        Assert.True(entriesPerSecond > 1000,
            $"Parsing speed too slow: {entriesPerSecond:F0} entries/sec (expected >1000)");
    }

    [Fact]
    public void EnumerateFiles_AllEntries_HaveValidPaths()
    {
        if (!IsAdmin || Drives.Count == 0) return;

        var indexer = new MftIndexer(Drives[0]);
        var entries = indexer.EnumerateFiles();

        var invalidEntries = entries.Where(e =>
            string.IsNullOrEmpty(e.FullPath) ||
            !e.FullPath.StartsWith(Drives[0] + ":\\")).ToList();

        Assert.Empty(invalidEntries);
    }

    private class TestProgress : IProgress<IndexProgress>
    {
        public List<IndexProgress> Reports { get; } = [];
        public List<IndexStage> Stages { get; } = [];

        public void Report(IndexProgress value)
        {
            Reports.Add(value);
            Stages.Add(value.Stage);
        }
    }
}
