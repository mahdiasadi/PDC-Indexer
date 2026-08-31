namespace ProjectIndexer.Core.Indexing;

internal class TrieNode
{
    private Dictionary<char, TrieNode>? _children;
    private List<int>? _entryIndices;

    public bool IsEndOfWord => _entryIndices is { Count: > 0 };

    public TrieNode GetOrAdd(char c)
    {
        _children ??= [];
        if (!_children.TryGetValue(c, out var next))
        {
            next = new TrieNode();
            _children[c] = next;
        }
        return next;
    }

    public TrieNode? TryGet(char c)
    {
        return _children != null && _children.TryGetValue(c, out var next) ? next : null;
    }

    public void AddEntry(int entryIndex)
    {
        _entryIndices ??= [];
        _entryIndices.Add(entryIndex);
    }

    public IReadOnlyList<int> EntryIndices => _entryIndices ?? [];

    public IEnumerable<TrieNode> Children
    {
        get
        {
            if (_children != null)
                return _children.Values;
            return [];
        }
    }

    public void ClearChildren()
    {
        _children = null;
    }
}

internal class Trie
{
    private readonly TrieNode _root = new();

    public void Insert(string key, int entryIndex)
    {
        var node = _root;
        foreach (char c in key)
        {
            node = node.GetOrAdd(char.ToLowerInvariant(c));
        }
        node.AddEntry(entryIndex);
    }

    public List<int> Search(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return [];

        var node = _root;
        foreach (char c in prefix)
        {
            node = node.TryGet(char.ToLowerInvariant(c));
            if (node == null)
                return [];
        }

        var results = new List<int>();
        CollectAllEntries(node, results);
        return results;
    }

    public bool ContainsPrefix(string prefix)
    {
        var node = _root;
        foreach (char c in prefix)
        {
            node = node.TryGet(char.ToLowerInvariant(c));
            if (node == null)
                return false;
        }
        return true;
    }

    private static void CollectAllEntries(TrieNode node, List<int> results)
    {
        results.AddRange(node.EntryIndices);
        foreach (var child in node.Children)
            CollectAllEntries(child, results);
    }

    public void Clear()
    {
        _root.ClearChildren();
    }

    public int NodeCount
    {
        get
        {
            int count = 1;
            CountNodes(_root, ref count);
            return count;
        }
    }

    private static void CountNodes(TrieNode node, ref int count)
    {
        foreach (var child in node.Children)
        {
            count++;
            CountNodes(child, ref count);
        }
    }
}
