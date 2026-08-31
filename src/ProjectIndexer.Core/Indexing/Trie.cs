namespace ProjectIndexer.Core.Indexing;

internal class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; } = new();
    public List<int> EntryIndices { get; } = new();
    public bool IsEndOfWord => EntryIndices.Count > 0;
}

internal class Trie
{
    private readonly TrieNode _root = new();

    public void Insert(string key, int entryIndex)
    {
        var node = _root;
        foreach (char c in key)
        {
            char lower = char.ToLowerInvariant(c);
            if (!node.Children.TryGetValue(lower, out var next))
            {
                next = new TrieNode();
                node.Children[lower] = next;
            }
            node = next;
        }
        node.EntryIndices.Add(entryIndex);
    }

    public List<int> Search(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return [];

        var node = _root;
        foreach (char c in prefix)
        {
            char lower = char.ToLowerInvariant(c);
            if (!node.Children.TryGetValue(lower, out var next))
                return [];
            node = next;
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
            char lower = char.ToLowerInvariant(c);
            if (!node.Children.TryGetValue(lower, out var next))
                return false;
            node = next;
        }
        return true;
    }

    private static void CollectAllEntries(TrieNode node, List<int> results)
    {
        results.AddRange(node.EntryIndices);
        foreach (var child in node.Children.Values)
            CollectAllEntries(child, results);
    }

    public void Clear()
    {
        _root.Children.Clear();
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
        foreach (var child in node.Children.Values)
        {
            count++;
            CountNodes(child, ref count);
        }
    }
}
