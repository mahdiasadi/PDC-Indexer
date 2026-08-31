using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;
using ProjectIndexer.Core.Searching;

namespace ProjectIndexer.Core.Indexing;

public class IndexEngine
{
    private readonly IFileSystemProvider _provider;
    private readonly IndexDatabase _database;
    private readonly InMemoryIndex _memoryIndex = new();
    private readonly SearchEngine _searchEngine;
    private bool _isIndexed;
    private int _entryCountSinceLastSave;

    public IFileSystemProvider Provider => _provider;
    public InMemoryIndex MemoryIndex => _memoryIndex;
    public char DriveLetter => _provider.DriveLetter;
    public bool IsIndexed => _isIndexed;
    public int EntryCount => _memoryIndex.Count;
    public Action<FileEntry>? OnEntryIndexed { get; set; }

    private const int SaveBatchSize = 5000;

    public IndexEngine(IFileSystemProvider provider, IndexDatabase? database = null)
    {
        _provider = provider;
        _database = database ?? new IndexDatabase();
        _searchEngine = new SearchEngine(_memoryIndex);
    }

    public List<FileEntry> BuildIndex(IProgress<IndexProgress>? progress = null, string? folderPath = null)
    {
        if (TryIncrementalUpdate(progress, folderPath))
            return _memoryIndex.Entries.Where(e => e != null).ToList();

        return FullBuildIndex(progress, folderPath);
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
            return LoadFromDatabase();

        var entries = _database.LoadIndex(DriveLetter);
        if (entries.Count == 0) return false;

        _memoryIndex.Clear();

        var frnToEntry = new Dictionary<ulong, FileEntry>(entries.Count);
        foreach (var e in entries)
            frnToEntry[e.Frn] = e;

        var added = new List<FileEntry>();
        var deleted = new HashSet<ulong>();

        foreach (var change in changes)
        {
            bool isDelete = change.Frn != 0 &&
                entries.Any(e => e.Frn == change.Frn &&
                    (e.Name == change.Name || e.Name.Equals(change.Name, StringComparison.OrdinalIgnoreCase)));

            if (isDelete)
            {
                deleted.Add(change.Frn);
                _database.DeleteEntry(change.Frn, DriveLetter);
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

        var allEntries = entries.Where(e => !deleted.Contains(e.Frn)).Concat(added).ToList();
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

    private List<FileEntry> FullBuildIndex(IProgress<IndexProgress>? progress, string? folderPath)
    {
        var progressInfo = _provider.CreateProgress();
        progressInfo.Stage = IndexStage.Starting;
        progress?.Report(progressInfo);

        _memoryIndex.Clear();
        _entryCountSinceLastSave = 0;

        using var dbConn = _database.OpenConnection();
        using var dbTxn = dbConn.BeginTransaction();

        _database.ClearDriveIndex(dbConn, DriveLetter);

        var entryBuffer = new List<FileEntry>();
        _provider.OnEntryIndexed = entry =>
        {
            if (folderPath != null && !entry.FullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                return;
            entryBuffer.Add(entry);
            OnEntryIndexed?.Invoke(entry);

            _entryCountSinceLastSave++;
            if (_entryCountSinceLastSave >= SaveBatchSize)
            {
                _database.AppendBatch(dbConn, entryBuffer, DriveLetter);
                entryBuffer.Clear();
                _entryCountSinceLastSave = 0;
            }
        };

        var entries = _provider.EnumerateFiles(progress);
        List<FileEntry> filteredEntries;
        if (folderPath != null)
        {
            filteredEntries = entries
                .Where(e => e.FullPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            filteredEntries = entries;
        }
        _memoryIndex.AddRange(filteredEntries);
        _isIndexed = true;

        _provider.OnEntryIndexed = null;

        if (entryBuffer.Count > 0)
        {
            _database.AppendBatch(dbConn, entryBuffer, DriveLetter);
            entryBuffer.Clear();
            _entryCountSinceLastSave = 0;
        }

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

    public List<FileEntry> Search(string query)
    {
        if (!_isIndexed || string.IsNullOrEmpty(query))
            return [];

        return _searchEngine.Execute(query);
    }

    public List<FileEntry> SearchByPrefix(string prefix)
    {
        if (!_isIndexed || string.IsNullOrEmpty(prefix))
            return [];

        return _memoryIndex.SearchByPrefix(prefix);
    }

    public List<FileEntry> SearchByWildcard(string pattern)
    {
        if (!_isIndexed || string.IsNullOrEmpty(pattern))
            return [];

        return _memoryIndex.WildcardSearch(pattern);
    }

    public FileEntry? GetByPath(string path)
    {
        return _memoryIndex.GetByPath(path);
    }

    public void SaveToDatabase()
    {
        if (!_isIndexed) return;

        _database.SaveIndex(_memoryIndex.Entries, DriveLetter);
    }

    public bool LoadFromDatabase()
    {
        if (!_database.HasIndex(DriveLetter))
            return false;

        var entries = _database.LoadIndex(DriveLetter);
        if (entries.Count == 0) return false;

        _memoryIndex.Clear();
        _memoryIndex.AddRange(entries);
        _isIndexed = true;
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
