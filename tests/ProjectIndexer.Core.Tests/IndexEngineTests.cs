using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Searching;

namespace ProjectIndexer.Core.Tests;

public class TrieTests
{
    [Fact]
    public void Trie_Search_Empty_ReturnsEmpty()
    {
        var trie = new Trie();
        var result = trie.Search("test");
        Assert.Empty(result);
    }

    [Fact]
    public void Trie_InsertAndSearch_ReturnsMatches()
    {
        var trie = new Trie();
        trie.Insert("hello", 0);
        trie.Insert("help", 1);
        trie.Insert("world", 2);

        var result = trie.Search("hel");
        Assert.Equal(2, result.Count);
        Assert.Contains(0, result);
        Assert.Contains(1, result);
    }

    [Fact]
    public void Trie_Search_CaseInsensitive()
    {
        var trie = new Trie();
        trie.Insert("Hello", 0);

        var result = trie.Search("hello");
        Assert.Single(result);
        Assert.Contains(0, result);
    }

    [Fact]
    public void Trie_ContainsPrefix_ReturnsCorrect()
    {
        var trie = new Trie();
        trie.Insert("windows", 0);

        Assert.True(trie.ContainsPrefix("win"));
        Assert.False(trie.ContainsPrefix("xyz"));
    }

    [Fact]
    public void Trie_Clear_RemovesAll()
    {
        var trie = new Trie();
        trie.Insert("test", 0);
        trie.Clear();
        Assert.Empty(trie.Search("test"));
    }
}

public class InMemoryIndexTests
{
    [Fact]
    public void AddRange_IncreasesCount()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "test.txt", FullPath = @"C:\test.txt", DriveLetter = 'C' },
            new FileEntry { Name = "hello.txt", FullPath = @"C:\hello.txt", DriveLetter = 'C' },
        ]);

        Assert.Equal(2, idx.Count);
    }

    [Fact]
    public void SearchByName_Exact_ReturnsMatch()
    {
        var idx = new InMemoryIndex();
        idx.Add(new FileEntry { Name = "exact.txt", FullPath = @"C:\exact.txt", DriveLetter = 'C' });

        var result = idx.SearchByName("exact.txt");
        Assert.Single(result);
    }

    [Fact]
    public void SearchByName_CaseInsensitive()
    {
        var idx = new InMemoryIndex();
        idx.Add(new FileEntry { Name = "Exact.TXT", FullPath = @"C:\file", DriveLetter = 'C' });

        var result = idx.SearchByName("exact.txt");
        Assert.Single(result);
    }

    [Fact]
    public void SearchByPrefix_ReturnsAllMatches()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "windows.exe", FullPath = @"C:\windows.exe", DriveLetter = 'C' },
            new FileEntry { Name = "winword.exe", FullPath = @"C:\winword.exe", DriveLetter = 'C' },
            new FileEntry { Name = "notepad.exe", FullPath = @"C:\notepad.exe", DriveLetter = 'C' },
        ]);

        var result = idx.SearchByPrefix("win");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void WildcardSearch_Star_MatchesPattern()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "test.txt", FullPath = @"C:\test.txt", DriveLetter = 'C' },
            new FileEntry { Name = "test.exe", FullPath = @"C:\test.exe", DriveLetter = 'C' },
            new FileEntry { Name = "readme.md", FullPath = @"C:\readme.md", DriveLetter = 'C' },
        ]);

        var result = idx.WildcardSearch("*.txt");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "test.txt");
    }

    [Fact]
    public void WildcardSearch_QuestionMark_MatchesPattern()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "file1.txt", FullPath = @"C:\file1.txt", DriveLetter = 'C' },
            new FileEntry { Name = "file2.txt", FullPath = @"C:\file2.txt", DriveLetter = 'C' },
            new FileEntry { Name = "file10.txt", FullPath = @"C:\file10.txt", DriveLetter = 'C' },
        ]);

        var result = idx.WildcardSearch("file?.txt");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetByPath_ReturnsCorrectEntry()
    {
        var idx = new InMemoryIndex();
        idx.Add(new FileEntry { Name = "doc.txt", FullPath = @"D:\docs\doc.txt", DriveLetter = 'D' });

        var entry = idx.GetByPath(@"D:\docs\doc.txt");
        Assert.NotNull(entry);
        Assert.Equal("doc.txt", entry.Name);
    }

    [Fact]
    public void GetByFrn_ReturnsCorrectEntry()
    {
        var idx = new InMemoryIndex();
        idx.Add(new FileEntry { Frn = 42, Name = "mftfile.dat", FullPath = @"C:\mftfile.dat", DriveLetter = 'C' });

        var entry = idx.GetByFrn(42);
        Assert.NotNull(entry);
        Assert.Equal("mftfile.dat", entry.Name);
    }

    [Fact]
    public void Filter_AppliesPredicate()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "a.txt", Size = 100, IsDirectory = false, DriveLetter = 'C' },
            new FileEntry { Name = "b.txt", Size = 1000, IsDirectory = false, DriveLetter = 'C' },
            new FileEntry { Name = "c.txt", Size = 500, IsDirectory = false, DriveLetter = 'C' },
        ]);

        var filtered = idx.Filter(e => e.Size > 200).ToList();
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void Clear_EmptiesIndex()
    {
        var idx = new InMemoryIndex();
        idx.Add(new FileEntry { Name = "test.txt", FullPath = @"C:\test.txt", DriveLetter = 'C' });
        idx.Clear();
        Assert.Equal(0, idx.Count);
    }
}

public class SearchEngineTests
{
    private static InMemoryIndex CreateTestIndex()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "windows.exe", FullPath = @"C:\Windows\windows.exe", Size = 100_000, IsDirectory = false, DriveLetter = 'C', LastModifiedTime = DateTime.UtcNow },
            new FileEntry { Name = "winword.exe", FullPath = @"C:\Program Files\winword.exe", Size = 200_000, IsDirectory = false, DriveLetter = 'C', IsHidden = false },
            new FileEntry { Name = "notepad.exe", FullPath = @"C:\Windows\notepad.exe", Size = 50_000, IsDirectory = false, DriveLetter = 'C' },
            new FileEntry { Name = "config.sys", FullPath = @"C:\config.sys", Size = 1_000, IsDirectory = false, IsSystem = true, DriveLetter = 'C' },
            new FileEntry { Name = "hidden.dll", FullPath = @"C:\Windows\System32\hidden.dll", Size = 300_000, IsDirectory = false, IsHidden = true, DriveLetter = 'C' },
            new FileEntry { Name = "readme.txt", FullPath = @"D:\readme.txt", Size = 500, IsDirectory = false, DriveLetter = 'D' },
            new FileEntry { Name = "Documents", FullPath = @"C:\Users\Documents", IsDirectory = true, DriveLetter = 'C' },
            new FileEntry { Name = "archive.zip", FullPath = @"E:\archive.zip", Size = 50_000_000, IsDirectory = false, DriveLetter = 'E' },
        ]);
        return idx;
    }

    [Fact]
    public void Execute_PrefixSearch_ReturnsMatches()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("win");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Execute_Wildcard_ReturnsMatches()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("*.txt");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "readme.txt");
    }

    [Fact]
    public void Execute_Regex_ReturnsMatches()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("regex:^win.*exe$");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Execute_SizeFilter_GreaterThan()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("*.exe size:>100000");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "winword.exe");
    }

    [Fact]
    public void Execute_SizeFilter_LessThan()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("size:<1000");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "readme.txt");
    }

    [Fact]
    public void Execute_DriveFilter_ReturnsDriveEntries()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("drive:d:");
        Assert.Single(result);
        Assert.Contains(result, e => e.DriveLetter == 'D');
    }

    [Fact]
    public void Execute_Negation_ExcludesMatches()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("*.exe !win");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "notepad.exe");
    }

    [Fact]
    public void Execute_Attribute_Hidden()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("hidden");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "hidden.dll");
    }

    [Fact]
    public void Execute_Attribute_Directory()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("directory");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "Documents");
    }

    [Fact]
    public void Execute_MultipleTerms_AndCondition()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("win *.exe");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Execute_EmptyQuery_ReturnsEmpty()
    {
        var engine = new SearchEngine(CreateTestIndex());
        Assert.Empty(engine.Execute(""));
        Assert.Empty(engine.Execute("   "));
    }

    [Fact]
    public void Execute_SingleTerm_PrefixFallback()
    {
        var engine = new SearchEngine(CreateTestIndex());
        var result = engine.Execute("not");
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "notepad.exe");
    }

    [Fact]
    public void Execute_DotExtension_ReturnsOnlyMatchingExtension()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "photo.jpg",   FullPath = @"C:\photo.jpg",   DriveLetter = 'C' },
            new FileEntry { Name = "image.JPG",   FullPath = @"C:\image.JPG",   DriveLetter = 'C' },
            new FileEntry { Name = "document.txt",FullPath = @"C:\doc.txt",     DriveLetter = 'C' },
            new FileEntry { Name = "notes.md",    FullPath = @"C:\notes.md",    DriveLetter = 'C' },
            new FileEntry { Name = "photo.jpeg",  FullPath = @"C:\photo.jpeg",  DriveLetter = 'C' },
            new FileEntry { Name = "script.js",   FullPath = @"C:\script.js",   DriveLetter = 'C' },
        ]);
        var engine = new SearchEngine(idx);

        var result = engine.Execute(".jpg");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "photo.jpg");
        Assert.Contains(result, e => e.Name == "image.JPG");
    }

    [Fact]
    public void Execute_DotExtension_FullPathLikeMft_StillWorks()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "photo.jpg",   FullPath = @"C:\Users\test\Pictures\photo.jpg",   DriveLetter = 'C' },
            new FileEntry { Name = "image.JPG",   FullPath = @"D:\Photos\Vacation\image.JPG",      DriveLetter = 'D' },
            new FileEntry { Name = "doc.txt",     FullPath = @"C:\docs\doc.txt",                   DriveLetter = 'C' },
            new FileEntry { Name = "readme.md",   FullPath = @"C:\readme.md",                      DriveLetter = 'C' },
            new FileEntry { Name = "notes.txt",   FullPath = @"C:\temp\.jpg_cache\notes.txt",      DriveLetter = 'C' },
        ]);
        var engine = new SearchEngine(idx);

        var result = engine.Execute(".jpg");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "photo.jpg");
        Assert.Contains(result, e => e.Name == "image.JPG");
    }

    [Fact]
    public void Execute_DotTxt_DoesNotMatchDotTxtInPath()
    {
        var idx = new InMemoryIndex();
        idx.AddRange([
            new FileEntry { Name = "settings.txt",  FullPath = @"C:\config\settings.txt",      DriveLetter = 'C' },
            new FileEntry { Name = "readme",        FullPath = @"C:\readme.txt.backup\readme", DriveLetter = 'C' },
        ]);
        var engine = new SearchEngine(idx);

        var result = engine.Execute(".txt");

        Assert.Single(result);
        Assert.Equal("settings.txt", result[0].Name);
    }

    [Fact]
    public void Search_ThroughIndexEngine_AfterBuildExtensionFilterWorks()
    {
        var idx = new InMemoryIndex();
        for (int i = 1; i <= 100; i++)
        {
            string ext = i % 3 == 0 ? "txt" : i % 3 == 1 ? "exe" : "dll";
            idx.Add(new FileEntry
            {
                Name = $"file{i}.{ext}",
                FullPath = $@"C:\Test\file{i}.{ext}",
                DriveLetter = 'C',
            });
        }
        var engine = new SearchEngine(idx);

        var result = engine.Execute(".txt");

        Assert.All(result, e => Assert.EndsWith(".txt", e.Name, StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Count > 0);
    }

    [Fact]
    public void Search_ThroughIndexEngine_AfterBuildContainsWorks()
    {
        var idx = new InMemoryIndex();
        for (int i = 1; i <= 100; i++)
        {
            string ext = i % 3 == 0 ? "txt" : i % 3 == 1 ? "exe" : "dll";
            idx.Add(new FileEntry
            {
                Name = $"file{i}.{ext}",
                FullPath = $@"C:\Test\file{i}.{ext}",
                DriveLetter = 'C',
            });
        }
        var engine = new SearchEngine(idx);

        var result = engine.Execute("file");

        Assert.Equal(100, result.Count);
    }
}

public class IndexEngineIntegrationTests
{
    [Fact]
    public void BuildIndex_WithMockProvider_PopulatesIndex()
    {
        var provider = new MockProvider();
        var engine = new IndexEngine(provider);

        var entries = engine.BuildIndex();
        Assert.NotEmpty(entries);
        Assert.True(engine.IsIndexed);
        Assert.True(engine.EntryCount > 0);
    }

    [Fact]
    public void Search_AfterBuild_ReturnsResults()
    {
        var provider = new MockProvider();
        var engine = new IndexEngine(provider);
        engine.BuildIndex();

        var result = engine.Search("file");
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Search_WithoutBuild_ReturnsEmpty()
    {
        var provider = new MockProvider();
        var engine = new IndexEngine(provider);

        Assert.Empty(engine.Search("test"));
    }

    [Fact]
    public void SaveAndLoadFromDatabase_PreservesEntries()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ProjectIndexer_Test_" + Guid.NewGuid());
        var db = new IndexDatabase(dbPath);
        var provider = new MockProvider('C');
        var engine = new IndexEngine(provider, db);

        engine.BuildIndex();
        engine.SaveToDatabase();

        var engine2 = new IndexEngine(new MockProvider('C'), db);
        bool loaded = engine2.LoadFromDatabase();

        Assert.True(loaded);
        Assert.True(engine2.IsIndexed);
        Assert.True(engine2.EntryCount > 0);

        // Cleanup
        try
        {
            Directory.Delete(dbPath, true);
        }
        catch
        {
            // Temp cleanup is best-effort
        }
    }

    [Fact]
    public void QuickSearch_ExactMode_ReturnsExact()
    {
        var provider = new MockProvider();
        var engine = new IndexEngine(provider);
        engine.BuildIndex();

        var result = engine.QuickSearch("file3.txt", SearchMode.Exact);
        Assert.Single(result);
        Assert.Contains(result, e => e.Name == "file3.txt");
    }

    [Fact]
    public void QuickSearch_WildcardMode_ReturnsPattern()
    {
        var provider = new MockProvider();
        var engine = new IndexEngine(provider);
        engine.BuildIndex();

        var result = engine.QuickSearch("*.txt", SearchMode.Wildcard);
        Assert.True(result.Count > 0);
        Assert.All(result, e => Assert.EndsWith(".txt", e.Name, StringComparison.OrdinalIgnoreCase));
    }

    private class MockProvider : IFileSystemProvider
    {
        public FileSystemType FileSystemType => FileSystemType.Ntfs;
        public char DriveLetter { get; }
        public bool SupportsJournaling => false;
        public Action<FileEntry>? OnEntryIndexed { get; set; }

        public MockProvider(char driveLetter = 'C')
        {
            DriveLetter = driveLetter;
        }

        public bool CanProcess() => true;

        public List<FileEntry> EnumerateFiles(IProgress<IndexProgress>? progress = null)
        {
            var entries = new List<FileEntry>();
            for (int i = 1; i <= 100; i++)
            {
                entries.Add(new FileEntry
                {
                    Frn = (ulong)i,
                    Name = $"file{i}.{(i % 3 == 0 ? "txt" : i % 3 == 1 ? "exe" : "dll")}",
                    FullPath = $@"C:\Test\file{i}.{(i % 3 == 0 ? "txt" : i % 3 == 1 ? "exe" : "dll")}",
                    Size = i * 1000,
                    IsDirectory = false,
                    DriveLetter = DriveLetter,
                    CreationTime = DateTime.UtcNow.AddDays(-i),
                    LastModifiedTime = DateTime.UtcNow.AddHours(-i),
                });
            }
            entries.Add(new FileEntry
            {
                Frn = 101,
                Name = "SubFolder",
                FullPath = @"C:\Test\SubFolder",
                IsDirectory = true,
                DriveLetter = DriveLetter,
            });
            return entries;
        }

        public IndexProgress CreateProgress() => new()
        {
            DriveLetter = DriveLetter.ToString(),
            Stage = IndexStage.Starting,
        };
    }
}
