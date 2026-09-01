using System.Collections.Concurrent;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Indexing;

public class InMemoryIndex
{
    private readonly Trie _nameTrie = new();
    private readonly Dictionary<string, List<int>> _nameIndex =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _pathIndex =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, int> _frnIndex = new();
    private readonly List<FileEntry> _entries = [];
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public IReadOnlyList<FileEntry> Entries
    {
        get { lock (_lock) return [.. _entries]; }
    }

    public void AddRange(IEnumerable<FileEntry> entries)
    {
        lock (_lock)
        {
            foreach (var entry in entries)
            {
                int index = _entries.Count;
                _entries.Add(entry);
                BuildNameIndex(entry, index);
            }
        }
    }

    public void Add(FileEntry entry)
    {
        lock (_lock)
        {
            int index = _entries.Count;
            _entries.Add(entry);
            BuildNameIndex(entry, index);
        }
    }

    private void BuildNameIndex(FileEntry entry, int index)
    {
        _nameTrie.Insert(entry.Name, index);

        if (!_nameIndex.TryGetValue(entry.Name, out var list))
        {
            list = [];
            _nameIndex[entry.Name] = list;
        }
        list.Add(index);

        if (!string.IsNullOrEmpty(entry.FullPath))
            _pathIndex.TryAdd(entry.FullPath, index);

        if (entry.Frn != 0)
            _frnIndex.TryAdd(entry.Frn, index);
    }

    public List<FileEntry> SearchByContains(string substring)
    {
        if (string.IsNullOrEmpty(substring)) return [];

        if (substring.StartsWith('.'))
        {
            lock (_lock)
                return _entries.Where(e => e.Name.EndsWith(substring, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        lock (_lock)
        {
            var results = new List<FileEntry>();
            foreach (var entry in _entries)
            {
                if (entry.Name.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    results.Add(entry);
            }
            return results;
        }
    }

    public List<FileEntry> SearchByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return [];

        lock (_lock)
        {
            if (_nameIndex.TryGetValue(name, out var indices))
            {
                var results = new List<FileEntry>(indices.Count);
                foreach (int i in indices)
                    results.Add(_entries[i]);
                return results;
            }
        }

        return [];
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

    public FileEntry? GetByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

        lock (_lock)
        {
            if (_pathIndex.TryGetValue(fullPath, out int index))
                return _entries[index];
        }

        return null;
    }

    public FileEntry? GetByFrn(ulong frn)
    {
        if (frn == 0) return null;

        lock (_lock)
        {
            if (_frnIndex.TryGetValue(frn, out int index))
                return _entries[index];
        }

        return null;
    }

    public List<FileEntry> WildcardSearch(string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return [];

        string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var regex = new System.Text.RegularExpressions.Regex(regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

        lock (_lock)
        {
            return _entries.Where(e => regex.IsMatch(e.Name)).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _nameTrie.Clear();
            _nameIndex.Clear();
            _pathIndex.Clear();
            _frnIndex.Clear();
            _entries.Clear();
        }
    }

    public IEnumerable<FileEntry> Filter(Func<FileEntry, bool> predicate)
    {
        lock (_lock)
        {
            return _entries.Where(predicate).ToList();
        }
    }
}
