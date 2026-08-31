using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Indexing;

public sealed class FastIndex : IDisposable
{
    private const uint MagicNumber = 0x46495844; // "DIXF" - Fast Index
    private const ushort Version = 1;
    private const int HeaderSize = 64;
    private const int EntrySize = 48;
    private const int NgramSize = 3;
    private const int MaxNgramsPerEntry = 50;

    private readonly string _indexPath;
    private readonly string _dataPath;
    private readonly string _ngramPath;
    private readonly string _dictPath;
    
    private readonly Dictionary<string, int> _pathDict = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _dictEntries = [];
    private readonly List<FileEntry> _entries = [];
    private readonly object _lock = new();
    
    private int _entryCount;
    private int _dictCount;
    private long _ngramCount;
    private bool _disposed;
    private bool _dirty;

    // In-memory structures for fast search
    private readonly Trie _nameTrie = new();
    private readonly Dictionary<string, List<int>> _nameIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _pathIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, int> _frnIndex = new();
    private readonly List<(uint hash, int entryIndex)> _ngramEntries = [];

    public int Count => _entryCount;
    public IReadOnlyList<FileEntry> Entries => _entries;
    public bool IsEmpty => _entryCount == 0;

    public FastIndex(string basePath)
    {
        _indexPath = basePath + ".idx";
        _dataPath = basePath + ".dat";
        _ngramPath = basePath + ".ngm";
        _dictPath = basePath + ".dict";
    }

    public static FastIndex CreateOrOpen(string basePath)
    {
        var index = new FastIndex(basePath);
        if (File.Exists(basePath + ".idx"))
        {
            index.Load();
        }
        return index;
    }

    public void AddRange(IEnumerable<FileEntry> entries)
    {
        lock (_lock)
        {
            foreach (var entry in entries)
            {
                Add(entry);
            }
        }
    }

    public void Add(FileEntry entry)
    {
        lock (_lock)
        {
            // Ensure FullPath is never null
            string fullPath = entry.FullPath ?? string.Empty;
            int pathId = GetOrAddPathId(fullPath);
            int entryIndex = _entries.Count;
            _entries.Add(entry);
            
            BuildInMemoryIndexes(entryIndex, entry, pathId);
            
            _entryCount++;
            _dirty = true;
        }
    }

    private void BuildInMemoryIndexes(int entryIndex, FileEntry entry, int pathId)
    {
        // Name trie for prefix search
        _nameTrie.Insert(entry.Name, entryIndex);
        
        // Exact name lookup
        if (!_nameIndex.TryGetValue(entry.Name, out var list))
        {
            list = [];
            _nameIndex[entry.Name] = list;
        }
        list.Add(entryIndex);
        
        // Full path lookup
        if (!string.IsNullOrEmpty(entry.FullPath))
            _pathIndex.TryAdd(entry.FullPath, entryIndex);
        
        // FRN lookup
        if (entry.Frn != 0)
            _frnIndex.TryAdd(entry.Frn, entryIndex);
        
        // N-grams for contains search
        BuildNgramsForEntry(entryIndex, entry.Name);
    }

    private int GetOrAddPathId(string path)
    {
        if (_pathDict.TryGetValue(path, out int id))
            return id;

        id = _dictEntries.Count;
        _dictEntries.Add(path);
        _pathDict[path] = id;
        _dictCount++;
        _dirty = true;
        return id;
    }

    private void BuildNgramsForEntry(int entryIndex, string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        
        string lower = name.ToLowerInvariant();
        if (lower.Length < NgramSize) return;

        int ngramCount = lower.Length - NgramSize + 1;
        ngramCount = Math.Min(ngramCount, MaxNgramsPerEntry);

        for (int i = 0; i < ngramCount; i++)
        {
            string ngram = lower.Substring(i, NgramSize);
            uint hash = Fnv1aHash(ngram);
            _ngramEntries.Add((hash, entryIndex));
        }
    }

    private static uint Fnv1aHash(string s)
    {
        uint hash = 2166136261;
        foreach (char c in s)
        {
            hash ^= (uint)c;
            hash *= 16777619;
        }
        return hash;
    }

    public List<FileEntry> SearchByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return [];
        
        lock (_lock)
        {
            var indices = _nameTrie.Search(prefix);
            var seen = new HashSet<int>();
            var results = new List<FileEntry>(indices.Count);
            
            foreach (int i in indices)
            {
                if (seen.Add(i))
                    results.Add(_entries[i]);
            }
            return results;
        }
    }

    public List<FileEntry> SearchByContains(string substring)
    {
        if (string.IsNullOrEmpty(substring)) return [];
        
        lock (_lock)
        {
            if (substring.Length < NgramSize)
            {
                return LinearContainsSearch(substring);
            }
            
            return NgramSearch(substring);
        }
    }

    private List<FileEntry> LinearContainsSearch(string substring)
    {
        var results = new List<FileEntry>();
        string lower = substring.ToLowerInvariant();
        
        foreach (var entry in _entries)
        {
            if (entry.Name.Contains(lower, StringComparison.OrdinalIgnoreCase))
                results.Add(entry);
        }
        return results;
    }

    private List<FileEntry> NgramSearch(string substring)
    {
        string lower = substring.ToLowerInvariant();
        if (lower.Length < NgramSize) return LinearContainsSearch(substring);

        var candidateCounts = new Dictionary<int, int>();
        
        for (int i = 0; i <= lower.Length - NgramSize; i++)
        {
            string ngram = lower.Substring(i, NgramSize);
            uint hash = Fnv1aHash(ngram);
            
            foreach (int entryIndex in FindNgramEntries(hash))
            {
                if (candidateCounts.TryGetValue(entryIndex, out int count))
                    candidateCounts[entryIndex] = count + 1;
                else
                    candidateCounts[entryIndex] = 1;
            }
        }

        int requiredMatches = lower.Length - NgramSize + 1;
        var results = new List<FileEntry>();
        
        foreach (var kvp in candidateCounts)
        {
            if (kvp.Value >= requiredMatches)
            {
                var entry = _entries[kvp.Key];
                if (entry.Name.Contains(lower, StringComparison.OrdinalIgnoreCase))
                    results.Add(entry);
            }
        }
        
        return results;
    }

    public List<FileEntry> SearchByWildcard(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return [];
        
        lock (_lock)
        {
            string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            var regex = new System.Text.RegularExpressions.Regex(regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);
            
            return _entries.Where(e => regex.IsMatch(e.Name)).ToList();
        }
    }

    public List<FileEntry> SearchByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return [];
        
        lock (_lock)
        {
            if (_nameIndex.TryGetValue(name, out var indices))
            {
                return indices.Select(i => _entries[i]).ToList();
            }
            return [];
        }
    }

    public FileEntry? GetByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        
        lock (_lock)
        {
            if (_pathIndex.TryGetValue(fullPath, out int index))
                return _entries[index];
            return null;
        }
    }

    public FileEntry? GetByFrn(ulong frn)
    {
        if (frn == 0) return null;
        
        lock (_lock)
        {
            if (_frnIndex.TryGetValue(frn, out int index))
                return _entries[index];
            return null;
        }
    }

    public void Save()
    {
        if (!_dirty || _disposed) return;
        
        lock (_lock)
        {
            WriteFiles();
            _dirty = false;
        }
    }

    private void WriteFiles()
    {
        // Write index file
        using var indexStream = new FileStream(_indexPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var indexWriter = new BinaryWriter(indexStream);
        
        indexWriter.Write(MagicNumber);
        indexWriter.Write(Version);
        indexWriter.Write(_entryCount);
        indexWriter.Write(_dictCount);
        indexWriter.Write(_ngramCount);
        indexWriter.Write(DateTime.UtcNow.ToFileTimeUtc());
        
        foreach (var entry in _entries)
        {
            indexWriter.Write(entry.Frn);
            indexWriter.Write(entry.ParentFrn);
            indexWriter.Write(entry.Size);
            indexWriter.Write(entry.AllocatedSize);
            indexWriter.Write(entry.CreationTime?.ToFileTimeUtc() ?? 0);
            indexWriter.Write(entry.LastModifiedTime?.ToFileTimeUtc() ?? 0);
            indexWriter.Write(entry.IsDirectory);
            indexWriter.Write(entry.IsHidden);
            indexWriter.Write(entry.IsReadOnly);
            indexWriter.Write(entry.IsSystem);
            indexWriter.Write(entry.IsArchive);
            indexWriter.Write(entry.IsTemporary);
            indexWriter.Write(entry.DriveLetter);
            indexWriter.Write((byte)0); // reserved
            
            int pathId = _pathDict.GetValueOrDefault(entry.FullPath, -1);
            indexWriter.Write(pathId);
        }
        
        // Write dictionary
        using var dictStream = new FileStream(_dictPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var dictWriter = new BinaryWriter(dictStream, System.Text.Encoding.UTF8, true);
        
        dictWriter.Write(_dictCount);
        foreach (string path in _dictEntries)
        {
            dictWriter.Write(path);
        }
        
        // Write n-gram index
        using var ngramStream = new FileStream(_ngramPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var ngramWriter = new BinaryWriter(ngramStream);
        
        ngramWriter.Write(_ngramEntries.Count);
        foreach (var (hash, entryIndex) in _ngramEntries)
        {
            ngramWriter.Write(hash);
            ngramWriter.Write(entryIndex);
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            ReadFiles();
        }
    }

    private void ReadFiles()
    {
        // Read index file
        using var indexStream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var indexReader = new BinaryReader(indexStream);
        
        uint magic = indexReader.ReadUInt32();
        if (magic != MagicNumber)
            throw new InvalidDataException("Invalid index file format");
        
        indexReader.ReadUInt16(); // version
        _entryCount = indexReader.ReadInt32();
        _dictCount = indexReader.ReadInt32();
        _ngramCount = indexReader.ReadInt64();
        indexReader.ReadInt64(); // timestamp
        
        _entries.Clear();
        _entries.Capacity = _entryCount;
        _nameTrie.Clear();
        _nameIndex.Clear();
        _pathIndex.Clear();
        _frnIndex.Clear();
        
        for (int i = 0; i < _entryCount; i++)
        {
            ulong frn = indexReader.ReadUInt64();
            ulong parentFrn = indexReader.ReadUInt64();
            long size = indexReader.ReadInt64();
            long allocSize = indexReader.ReadInt64();
            long created = indexReader.ReadInt64();
            long modified = indexReader.ReadInt64();
            bool isDir = indexReader.ReadBoolean();
            bool isHidden = indexReader.ReadBoolean();
            bool isReadOnly = indexReader.ReadBoolean();
            bool isSystem = indexReader.ReadBoolean();
            bool isArchive = indexReader.ReadBoolean();
            bool isTemporary = indexReader.ReadBoolean();
            char driveLetter = indexReader.ReadChar();
            indexReader.ReadByte(); // reserved
            int pathId = indexReader.ReadInt32();
            
            string fullPath = pathId >= 0 && pathId < _dictEntries.Count 
                ? _dictEntries[pathId] 
                : "";

            var entry = new FileEntry
            {
                Frn = frn,
                ParentFrn = parentFrn,
                Size = size,
                AllocatedSize = allocSize,
                CreationTime = created != 0 ? DateTime.FromFileTimeUtc(created) : null,
                LastModifiedTime = modified != 0 ? DateTime.FromFileTimeUtc(modified) : null,
                IsDirectory = isDir,
                IsHidden = isHidden,
                IsReadOnly = isReadOnly,
                IsSystem = isSystem,
                IsArchive = isArchive,
                IsTemporary = isTemporary,
                DriveLetter = driveLetter,
                FullPath = fullPath,
            };
            
            _entries.Add(entry);
            BuildInMemoryIndexes(i, entry, pathId);
        }
        
        // Read dictionary
        using var dictStream = new FileStream(_dictPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var dictReader = new BinaryReader(dictStream, System.Text.Encoding.UTF8, true);
        
        _dictCount = dictReader.ReadInt32();
        _dictEntries.Clear();
        _pathDict.Clear();
        
        for (int i = 0; i < _dictCount; i++)
        {
            string path = dictReader.ReadString();
            _dictEntries.Add(path);
            _pathDict[path] = i;
        }
        
        // Read n-gram entries
        using var ngramStream = new FileStream(_ngramPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var ngramReader = new BinaryReader(ngramStream);
        
        int ngramCount = ngramReader.ReadInt32();
        _ngramEntries.Clear();
        _ngramEntries.Capacity = ngramCount;
        
        for (int i = 0; i < ngramCount; i++)
        {
            uint hash = ngramReader.ReadUInt32();
            int entryIndex = ngramReader.ReadInt32();
            _ngramEntries.Add((hash, entryIndex));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _nameTrie.Clear();
            _nameIndex.Clear();
            _pathIndex.Clear();
            _frnIndex.Clear();
            _ngramEntries.Clear();
            _pathDict.Clear();
            _dictEntries.Clear();
            _entryCount = 0;
            _dictCount = 0;
            _ngramCount = 0;
            _dirty = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        Save();
        _disposed = true;
    }

    private IEnumerable<int> FindNgramEntries(uint hash)
    {
        foreach (var (h, entryIndex) in _ngramEntries)
        {
            if (h == hash)
                yield return entryIndex;
        }
    }
}