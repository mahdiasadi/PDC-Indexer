using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;
using ProjectIndexer.Core.Searching;
using System.IO;

namespace ProjectIndexer.Core.Indexing;

public class IndexEngine
{
    private readonly IFileSystemProvider _provider;
    private readonly IndexDatabase _database;
    private readonly FastIndex _fastIndex;
    private readonly InMemoryIndex _memoryIndex = new();
    private readonly SearchEngine _searchEngine;
    private bool _isIndexed;
    private bool _isBuilding;
    private readonly string _fastIndexPath;

    public IFileSystemProvider Provider => _provider;
    public InMemoryIndex MemoryIndex => _memoryIndex;
    public FastIndex FastIndex => _fastIndex;
    public char DriveLetter => _provider.DriveLetter;
    public bool IsIndexed => _isIndexed;
    public int EntryCount => _fastIndex.Count;
    public Action<FileEntry>? OnEntryIndexed { get; set; }

    public IndexEngine(IFileSystemProvider provider, IndexDatabase? database = null, string? fastIndexPath = null)
    {
        _provider = provider;
        _database = database ?? new IndexDatabase();
        _fastIndexPath = fastIndexPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer", "FastIndex", $"{_provider.DriveLetter}:");
        
        string? indexDir = Path.GetDirectoryName(_fastIndexPath);
        if (!string.IsNullOrEmpty(indexDir))
            Directory.CreateDirectory(indexDir);
        
        _fastIndex = FastIndex.CreateOrOpen(_fastIndexPath);
        _searchEngine = new SearchEngine(_memoryIndex);
    }

    public List<FileEntry> BuildIndex(IProgress<IndexProgress>? progress = null, string? folderPath = null)
    {
        if (TryLoadFastIndex())
        {
            if (TryIncrementalUpdate(progress, folderPath))
                return _fastIndex.Entries.Where(e => e != null).ToList();
        }

        return FullBuildIndex(progress, folderPath);
    }

    private bool TryLoadFastIndex()
    {
        if (!_fastIndex.IsEmpty)
        {
            _isIndexed = true;
            SyncMemoryIndexFromFastIndex();
            return true;
        }
        return false;
    }

    private void SyncMemoryIndexFromFastIndex()
    {
        _memoryIndex.Clear();
        foreach (var entry in _fastIndex.Entries)
        {
            _memoryIndex.Add(entry);
        }
    }

    private bool TryIncrementalUpdate(IProgress<IndexProgress>? progress, string? folderPath)
    {
        if (folderPath != null) return false;
        if (!_provider.SupportsJournaling) return false;
        if (!_database.HasIndex(DriveLetter)) return false;
        if (_provider is not MftIndexer mft) return false;
        if (!_database.LoadJournalState(DriveLetter, out long journalId, out long nextUsn)) return false;

        var progressInfo = _provider.CreateProgress();
        progressInfo.Stage = IndexStage.IncrementalUpdate;
        progress?.Report(progressInfo);

        var changes = mft.GetJournalChanges(nextUsn, journalId);
        if (changes.Count == 0)
            return true;

        var frnToEntry = new Dictionary<ulong, FileEntry>(_fastIndex.Count);
        foreach (var e in _fastIndex.Entries)
            frnToEntry[e.Frn] = e;

        var added = new List<FileEntry>();
        var deleted = new HashSet<ulong>();

        foreach (var change in changes)
        {
            bool isDelete = change.Frn != 0 &&
                frnToEntry.ContainsKey(change.Frn);

            if (isDelete)
            {
                deleted.Add(change.Frn);
                _database.DeleteEntry(change.Frn, DriveLetter);
                _fastIndex.Clear(); // Will rebuild below
            }
            else
            {
                if (!frnToEntry.TryGetValue(change.ParentFrn, out var parent))
                {
                    if (change.ParentFrn != 0)
                        continue;
                }

                string parentPath = parent?.FullPath ?? DriveLetter + @":\";
                string fullPath = string.Concat(parentPath, change.Name);

                var entry = new FileEntry
                {
                    Frn = change.Frn,
                    ParentFrn = change.ParentFrn,
                    Name = change.Name,
                    FullPath = fullPath,
                    IsDirectory = change.IsDirectory,
                    Size = 0,
                    DriveLetter = DriveLetter,
                };

                frnToEntry[entry.Frn] = entry;
                added.Add(entry);
                _database.InsertOrUpdateEntry(entry, DriveLetter);
                OnEntryIndexed?.Invoke(entry);
            }
        }

        // Rebuild fast index from updated entries
        var allEntries = frnToEntry.Values.Where(e => !deleted.Contains(e.Frn)).Concat(added).ToList();
        RebuildFastIndex(allEntries);
        _memoryIndex.Clear();
        _memoryIndex.AddRange(allEntries);
        _isIndexed = true;

        var newJournalId = journalId;
        long newNextUsn = nextUsn;
        try
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
            if (!volHandle.IsInvalid)
            {
                var jd = UsnJournal.QueryJournal(volHandle);
                newJournalId = jd.UsnJournalId;
                newNextUsn = jd.NextUsn;
            }
        }
        catch { }

        _database.SaveJournalState(DriveLetter, newJournalId, newNextUsn);

        progressInfo.Stage = IndexStage.Completed;
        progressInfo.FilesFound = allEntries.Count(e => !e.IsDirectory);
        progressInfo.DirectoriesFound = allEntries.Count(e => e.IsDirectory);
        progress?.Report(progressInfo);

        return true;
    }

    private void RebuildFastIndex(IEnumerable<FileEntry> entries)
    {
        _fastIndex.Clear();
        _fastIndex.AddRange(entries);
        _fastIndex.Save();
    }

    private List<FileEntry> FullBuildIndex(IProgress<IndexProgress>? progress, string? folderPath)
    {
        var progressInfo = _provider.CreateProgress();
        progressInfo.Stage = IndexStage.Starting;
        progress?.Report(progressInfo);

        _isBuilding = true;
        try
        {
            _fastIndex.Clear();
            _memoryIndex.Clear();

            using var dbConn = _database.OpenConnection();
            using var dbTxn = dbConn.BeginTransaction();

            _database.ClearDriveIndex(dbConn, DriveLetter);

            var engineProgress = new Progress<IndexProgress>(p =>
            {
                p.DriveLetter = DriveLetter.ToString();
                progress?.Report(p);
            });

            _provider.OnEntryIndexed = entry =>
            {
                if (folderPath != null && !string.IsNullOrEmpty(entry.FullPath) && !entry.FullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                    return;

                OnEntryIndexed?.Invoke(entry);
            };

            var entries = _provider.EnumerateFiles(engineProgress);
            List<FileEntry> filteredEntries = folderPath != null
                ? entries.Where(e => !string.IsNullOrEmpty(e.FullPath) && e.FullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)).ToList()
                : entries.Where(e => !string.IsNullOrEmpty(e.FullPath)).ToList();

            _fastIndex.AddRange(filteredEntries);
            _memoryIndex.AddRange(filteredEntries);

            _database.AppendBatch(dbConn, filteredEntries, DriveLetter);

            _fastIndex.Save();
            _isIndexed = true;

            _provider.OnEntryIndexed = null;

            _database.SetMetadata(dbConn, $"LastIndexTime_{DriveLetter}", DateTime.UtcNow.ToString("O"));

            if (_provider.SupportsJournaling && _provider is MftIndexer mft)
            {
                try
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
                    if (!volHandle.IsInvalid)
                    {
                        var jd = UsnJournal.QueryJournal(volHandle);
                        _database.SetMetadata(dbConn, $"UsnJournalId_{DriveLetter}", jd.UsnJournalId.ToString());
                        _database.SetMetadata(dbConn, $"UsnNextUsn_{DriveLetter}", jd.NextUsn.ToString());
                    }
                }
                catch { }
            }

            dbTxn.Commit();

            progressInfo.Stage = IndexStage.Completed;
            progressInfo.FilesFound = filteredEntries.Count(e => !e.IsDirectory);
            progressInfo.DirectoriesFound = filteredEntries.Count(e => e.IsDirectory);
            progress?.Report(progressInfo);

            return filteredEntries;
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private bool CanSearch => _isIndexed || _isBuilding;

    public List<FileEntry> Search(string query)
    {
        if (string.IsNullOrEmpty(query)) return [];
        if (!CanSearch) return [];
        if (_fastIndex.IsEmpty) return [];

        // Use fast index for primary search, fallback to search engine for complex queries
        if (IsSimpleContainsQuery(query))
        {
            return _fastIndex.SearchByContains(query);
        }

        // For multi-word queries without special operators, use OR logic on fast index
        if (IsMultiWordSimpleQuery(query))
        {
            return SearchMultiWordOr(query);
        }

        return _searchEngine.Execute(query);
    }

    private static bool IsSimpleContainsQuery(string query)
    {
        query = query.Trim();
        if (query.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.Contains(' ')) return false;
        if (query.Contains('|')) return false;
        if (query.StartsWith('!')) return false;
        if (query.StartsWith('"')) return false;
        if (query.StartsWith("size:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("modified:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("created:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("content:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("drive:", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool IsMultiWordSimpleQuery(string query)
    {
        query = query.Trim();
        if (query.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.Contains('|')) return false;
        if (query.StartsWith('!')) return false;
        if (query.StartsWith('"')) return false;
        if (query.StartsWith("size:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("modified:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("created:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("content:", StringComparison.OrdinalIgnoreCase)) return false;
        if (query.StartsWith("drive:", StringComparison.OrdinalIgnoreCase)) return false;
        return query.Contains(' ');
    }

    private List<FileEntry> SearchMultiWordOr(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allResults = new HashSet<FileEntry>();

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            var results = _fastIndex.SearchByContains(word);
            foreach (var r in results)
                allResults.Add(r);
        }

        return allResults.ToList();
    }

    public List<FileEntry> SearchByPrefix(string prefix)
    {
        if (!CanSearch || string.IsNullOrEmpty(prefix))
            return [];

        return _fastIndex.SearchByPrefix(prefix);
    }

    public List<FileEntry> SearchByWildcard(string pattern)
    {
        if (!CanSearch || string.IsNullOrEmpty(pattern))
            return [];

        return _fastIndex.SearchByWildcard(pattern);
    }

    public FileEntry? GetByPath(string path)
    {
        return _fastIndex.GetByPath(path);
    }

    public void SaveToDatabase()
    {
        if (!_isIndexed) return;

        _database.SaveIndex(_fastIndex.Entries, DriveLetter);
        _fastIndex.Save();
    }

    public bool LoadFromDatabase()
    {
        if (!_database.HasIndex(DriveLetter))
            return false;

        var entries = _database.LoadIndex(DriveLetter);
        if (entries.Count == 0) return false;

        RebuildFastIndex(entries);
        _memoryIndex.Clear();
        _memoryIndex.AddRange(entries);
        _isIndexed = true;
        return true;
    }

    public bool LoadFromFastIndex()
    {
        if (_fastIndex.IsEmpty)
            return false;

        _isIndexed = true;
        SyncMemoryIndexFromFastIndex();
        return true;
    }

    public DateTime? GetLastIndexTime()
    {
        return _database.GetLastIndexTime(DriveLetter);
    }

    public long GetDatabaseEntryCount()
    {
        return _database.GetEntryCount(DriveLetter);
    }

    public void Clear()
    {
        _memoryIndex.Clear();
        _isIndexed = false;
    }

    public List<FileEntry> QuickSearch(string query, SearchMode mode = SearchMode.Prefix)
    {
        if (!_isIndexed) return [];

        return mode switch
        {
            SearchMode.Exact => _memoryIndex.SearchByName(query),
            SearchMode.Prefix => _memoryIndex.SearchByPrefix(query),
            SearchMode.Wildcard => _memoryIndex.WildcardSearch(query),
            SearchMode.FullText => _searchEngine.Execute(query),
            _ => _memoryIndex.SearchByPrefix(query),
        };
    }
}

public enum SearchMode
{
    Exact,
    Prefix,
    Wildcard,
    FullText,
}
