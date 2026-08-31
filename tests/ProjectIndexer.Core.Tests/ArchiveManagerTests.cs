using ProjectIndexer.Core.Archiving;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Tests;

public class ArchiveManagerTests
{
    private static string GetTestFolder()
    {
        return Path.Combine(Path.GetTempPath(), "ArchiveTest_" + Guid.NewGuid());
    }

    private static List<FileEntry> CreateTestEntries()
    {
        return
        [
            new() { Name = "doc1.txt", FullPath = @"C:\docs\doc1.txt", Size = 100, IsDirectory = false, DriveLetter = 'C', LastModifiedTime = DateTime.UtcNow },
            new() { Name = "doc2.txt", FullPath = @"C:\docs\doc2.txt", Size = 200, IsDirectory = false, DriveLetter = 'C', LastModifiedTime = DateTime.UtcNow },
            new() { Name = "image.png", FullPath = @"C:\images\image.png", Size = 5000, IsDirectory = false, DriveLetter = 'C' },
            new() { Name = "Programs", FullPath = @"D:\Programs", IsDirectory = true, DriveLetter = 'D' },
            new() { Name = "app.exe", FullPath = @"D:\Programs\app.exe", Size = 100000, IsDirectory = false, DriveLetter = 'D' },
        ];
    }

    [Fact]
    public void Constructor_CreatesArchiveFolder()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void CreateArchive_ReturnsFilePath()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            var entries = CreateTestEntries();
            string path = mgr.CreateArchive('C', entries);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.EndsWith(".archive", path);
            Assert.StartsWith("C_", Path.GetFileName(path));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ListArchives_ReturnsCreatedArchives()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            mgr.CreateArchive('C', CreateTestEntries());
            mgr.CreateArchive('D', CreateTestEntries());

            var archives = mgr.ListArchives();

            Assert.Equal(2, archives.Count);
            Assert.All(archives, a => Assert.True(a.EntryCount > 0));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ListArchives_WithDriveFilter_ReturnsOnlyThatDrive()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            mgr.CreateArchive('C', CreateTestEntries());
            mgr.CreateArchive('D', CreateTestEntries());

            var archives = mgr.ListArchives('C');

            Assert.Single(archives);
            Assert.Equal('C', archives[0].DriveLetter);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void LoadArchive_ReturnsAllEntries()
    {
        var folder = GetTestFolder();
        try
        {
            // CreateTestEntries returns 5 entries total (3 for C, 2 for D)
            var entries = CreateTestEntries();
            Assert.Equal(5, entries.Count);

            var mgr = new ArchiveManager(folder);
            string path = mgr.CreateArchive('C', entries);

            var loaded = mgr.LoadArchive(path);

            Assert.Equal(5, loaded.Count);
            Assert.Contains(loaded, e => e.Name == "doc1.txt");
            Assert.Contains(loaded, e => e.Name == "image.png");
            Assert.Contains(loaded, e => e.Name == "app.exe");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void SearchArchive_ReturnsFilteredResults()
    {
        var folder = GetTestFolder();
        try
        {
            var entries = CreateTestEntries(); // 5 entries, 2 are .txt

            var mgr = new ArchiveManager(folder);
            string path = mgr.CreateArchive('C', entries);

            var results = mgr.SearchArchive(path, "*.txt");

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.EndsWith(".txt", r.Name));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void SearchAllArchives_SearchesAcrossAll()
    {
        var folder = GetTestFolder();
        try
        {
            // CreateTestEntries has 2 .txt files - both archives will contain copies
            var entries = CreateTestEntries();

            var mgr = new ArchiveManager(folder);
            mgr.CreateArchive('C', entries);
            mgr.CreateArchive('D', entries);

            var results = mgr.SearchAllArchives("*.txt");

            // 2 archives × 2 .txt files each = 4 results
            Assert.Equal(4, results.Count);
            Assert.All(results, r => Assert.EndsWith(".txt", r.Entry.Name));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void DeleteArchive_RemovesFile()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            string path = mgr.CreateArchive('C', CreateTestEntries());
            Assert.True(File.Exists(path));

            mgr.DeleteArchive(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void MergeArchives_KeepsSpecifiedCount()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            mgr.CreateArchive('C', CreateTestEntries());
            Thread.Sleep(50);
            mgr.CreateArchive('C', CreateTestEntries());
            Thread.Sleep(50);
            mgr.CreateArchive('C', CreateTestEntries());
            Thread.Sleep(50);
            mgr.CreateArchive('C', CreateTestEntries());

            int count = mgr.ListArchives('C').Count;
            Assert.True(count >= 4, $"Expected at least 4 archives, got {count}");

            mgr.MergeArchives('C', keepCount: 2);

            Assert.Equal(2, mgr.ListArchives('C').Count);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void GetArchiveCount_ReturnsCorrectNumber()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            Assert.Equal(0, mgr.GetArchiveCount());

            mgr.CreateArchive('C', CreateTestEntries());
            Assert.Equal(1, mgr.GetArchiveCount());
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void LoadArchive_NonExistent_Throws()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            Assert.Throws<FileNotFoundException>(() => mgr.LoadArchive("nonexistent.archive"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void SearchArchive_NonExistent_Throws()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            Assert.Throws<FileNotFoundException>(() => mgr.SearchArchive("missing.archive", "test"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ArchiveInfo_DisplayName_FormatsCorrectly()
    {
        var info = new ArchiveInfo
        {
            DriveLetter = 'E',
            CreatedAt = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            EntryCount = 1000,
        };

        Assert.Contains("E:", info.DisplayName);
        Assert.Contains("2025-06-15", info.DisplayName);
        Assert.Contains("1,000", info.DisplayName);
    }

    [Fact]
    public void CreateArchive_PreservesEntryProperties()
    {
        var folder = GetTestFolder();
        try
        {
            var mgr = new ArchiveManager(folder);
            var original = new FileEntry
            {
                Name = "preserve_test.exe",
                FullPath = @"C:\test\preserve_test.exe",
                Size = 99999,
                IsDirectory = false,
                IsHidden = true,
                IsReadOnly = false,
                IsSystem = true,
                IsArchive = false,
                IsTemporary = false,
                DriveLetter = 'C',
                Frn = 12345,
                ParentFrn = 999,
                CreationTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            };

            string path = mgr.CreateArchive('C', [original]);
            var loaded = mgr.LoadArchive(path);

            Assert.Single(loaded);
            var entry = loaded[0];
            Assert.Equal(original.Name, entry.Name);
            Assert.Equal(original.FullPath, entry.FullPath);
            Assert.Equal(original.Size, entry.Size);
            Assert.Equal(original.IsHidden, entry.IsHidden);
            Assert.Equal(original.IsSystem, entry.IsSystem);
            Assert.Equal(original.Frn, entry.Frn);
            Assert.Equal(original.ParentFrn, entry.ParentFrn);
            Assert.NotNull(entry.CreationTime);
            Assert.NotNull(entry.LastModifiedTime);
            // Compare with 1-second tolerance due to string serialization
            Assert.Equal(original.CreationTime!.Value.ToUniversalTime().Ticks / 10000000,
                         entry.CreationTime.Value.ToUniversalTime().Ticks / 10000000);
            Assert.Equal(original.LastModifiedTime!.Value.ToUniversalTime().Ticks / 10000000,
                         entry.LastModifiedTime.Value.ToUniversalTime().Ticks / 10000000);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
