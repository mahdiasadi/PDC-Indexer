using System.Text.RegularExpressions;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Searching;

public class SearchEngine
{
    private readonly InMemoryIndex _index;

    public SearchEngine(InMemoryIndex index)
    {
        _index = index;
    }

    public List<FileEntry> Execute(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        query = query.Trim();

        if (query.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            return RegexSearch(query[6..]);

        var terms = ParseQuery(query);

        if (terms.Count == 0)
            return [];

        if (terms.Count == 1 && terms[0].Type == SearchTermType.Name
            && !terms[0].IsNegation && terms[0].PathPrefix == null
            && terms[0].OrValues == null && !terms[0].IsQuoted)
        {
            string val = terms[0].Value;
            var lower = val.ToLowerInvariant();
            if (lower is "hidden" or "readonly" or "system" or "directory" or "dir"
                or "file" or "archive" or "temporary")
                return ApplyAttributeFilter(_index.Entries, lower).ToList();

            if (val.Contains('*') || val.Contains('?'))
                return _index.WildcardSearch(val);

            if (val.StartsWith('.'))
                return _index.Filter(e => e.Name.EndsWith(val, StringComparison.OrdinalIgnoreCase)).ToList();

            return _index.SearchByContains(val);
        }

        return FilterByTerms(terms);
    }

    private static List<SearchTerm> ParseQuery(string query)
    {
        var terms = new List<SearchTerm>();
        int i = 0;

        while (i < query.Length)
        {
            if (char.IsWhiteSpace(query[i])) { i++; continue; }

            if (query[i] == '"')
            {
                int end = query.IndexOf('"', i + 1);
                if (end < 0) end = query.Length;
                string quoted = query[(i + 1)..end];
                if (quoted.Length > 0)
                {
                    string? pathPrefix = null;
                    int bs = quoted.IndexOf('\\');
                    if (bs >= 0)
                    {
                        pathPrefix = quoted[..bs].TrimEnd('\\');
                        quoted = quoted[(bs + 1)..].TrimStart('\\');
                    }
                    if (quoted.Length > 0)
                        terms.Add(new SearchTerm { Value = quoted, IsQuoted = true, PathPrefix = pathPrefix, Type = SearchTermType.Name });
                }
                i = end + 1;
                continue;
            }

            int space = query.IndexOf(' ', i);
            string part = space < 0 ? query[i..] : query[i..space];
            i = space < 0 ? query.Length : space + 1;

            if (string.IsNullOrEmpty(part)) continue;

            if (part.StartsWith('!'))
                terms.Add(new SearchTerm { Value = part[1..], IsNegation = true, Type = SearchTermType.Name });
            else if (part.StartsWith("size:", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = part[5..], Type = SearchTermType.Size });
            else if (part.StartsWith("modified:", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = part[9..], Type = SearchTermType.Modified });
            else if (part.StartsWith("created:", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = part[8..], Type = SearchTermType.Created });
            else if (part.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = part[8..], Type = SearchTermType.Content, ContentValue = part[8..] });
            else if (part.StartsWith("drive:", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = part[6..], Type = SearchTermType.Drive });
            else if (part.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = "hidden", Type = SearchTermType.Attribute });
            else if (part.Equals("readonly", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = "readonly", Type = SearchTermType.Attribute });
            else if (part.Equals("system", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = "system", Type = SearchTermType.Attribute });
            else if (part.Equals("directory", StringComparison.OrdinalIgnoreCase) || part.Equals("dir", StringComparison.OrdinalIgnoreCase))
                terms.Add(new SearchTerm { Value = "directory", Type = SearchTermType.Attribute });
            else
            {
                string[] orParts = part.Split('|', StringSplitOptions.RemoveEmptyEntries);
                string? pathPrefix = null;
                string baseValue = part;

                int bs = part.IndexOf('\\');
                if (bs >= 0 && !part.Contains('*') && !part.Contains('?'))
                {
                    pathPrefix = part[..bs].TrimEnd('\\');
                    baseValue = part[(bs + 1)..].TrimStart('\\');
                    if (string.IsNullOrEmpty(baseValue)) baseValue = part;
                }
                else if (bs >= 0)
                {
                    pathPrefix = part[..bs].TrimEnd('\\');
                    baseValue = part[(bs + 1)..].TrimStart('\\');
                    if (string.IsNullOrEmpty(baseValue)) baseValue = part;
                }

                if (orParts.Length > 1)
                {
                    terms.Add(new SearchTerm { Value = baseValue, OrValues = orParts, PathPrefix = pathPrefix, Type = SearchTermType.Name });
                }
                else
                {
                    terms.Add(new SearchTerm { Value = baseValue, PathPrefix = pathPrefix, Type = SearchTermType.Name });
                }
            }
        }

        return terms;
    }

    private List<FileEntry> RegexSearch(string pattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return _index.Filter(e => regex.IsMatch(e.Name)).ToList();
        }
        catch (RegexParseException)
        {
            return [];
        }
    }

    private List<FileEntry> FilterByTerms(List<SearchTerm> terms)
    {
        IEnumerable<FileEntry>? results = null;
        var nameTerms = terms.Where(t => t.Type == SearchTermType.Name && !t.IsNegation).ToList();
        var negateTerms = terms.Where(t => t.IsNegation).ToList();
        var filterTerms = terms.Where(t => t.Type != SearchTermType.Name).ToList();

        if (nameTerms.Count > 0)
        {
            List<List<FileEntry>> matchSets = [];
            foreach (var term in nameTerms)
            {
                List<FileEntry> matches;

                if (term.OrValues != null)
                {
                    matches = [];
                    var seen = new HashSet<FileEntry>();
                    foreach (var orVal in term.OrValues)
                    {
                        var orMatches = orVal.Contains('*') || orVal.Contains('?')
                            ? _index.WildcardSearch(orVal)
                            : orVal.StartsWith('.')
                                ? _index.Filter(e => e.Name.EndsWith(orVal, StringComparison.OrdinalIgnoreCase))
                                : _index.SearchByContains(orVal);
                        foreach (var m in orMatches)
                            if (seen.Add(m)) matches.Add(m);
                    }
                }
                else if (term.Value.Contains('*') || term.Value.Contains('?'))
                {
                    matches = _index.WildcardSearch(term.Value);
                }
                else if (term.IsQuoted)
                {
                    matches = _index.SearchByName(term.Value);
                }
                else if (term.Value.StartsWith('.'))
                {
                    matches = _index.Filter(e => e.Name.EndsWith(term.Value, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else
                {
                    matches = _index.SearchByContains(term.Value);
                }

                if (term.PathPrefix != null && matches.Count > 0)
                {
                    string prefix = term.PathPrefix.Replace('/', '\\');
                    matches = matches.Where(e =>
                        e.FullPath.Contains(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (matches.Count > 0)
                    matchSets.Add(matches);
            }

            if (matchSets.Count == 0)
                return [];

            results = matchSets[0];
            for (int i = 1; i < matchSets.Count; i++)
                results = results.Intersect(matchSets[i]).ToList();
        }
        else
        {
            results = _index.Entries;
        }

        if (results == null) return [];

        var filtered = results.AsEnumerable();

        foreach (var term in negateTerms)
        {
            var negate = term.Value.Contains('*') || term.Value.Contains('?')
                ? _index.WildcardSearch(term.Value)
                : term.Value.StartsWith('.')
                    ? _index.Filter(e => e.Name.EndsWith(term.Value, StringComparison.OrdinalIgnoreCase))
                    : _index.SearchByContains(term.Value);

            var negateSet = new HashSet<FileEntry>(negate);
            filtered = filtered.Where(e => !negateSet.Contains(e));
        }

        foreach (var term in filterTerms)
        {
            filtered = term.Type switch
            {
                SearchTermType.Size => ApplySizeFilter(filtered, term.Value),
                SearchTermType.Modified => ApplyDateFilter(filtered, term.Value, e => e.LastModifiedTime),
                SearchTermType.Created => ApplyDateFilter(filtered, term.Value, e => e.CreationTime),
                SearchTermType.Drive => ApplyDriveFilter(filtered, term.Value),
                SearchTermType.Attribute => ApplyAttributeFilter(filtered, term.Value),
                _ => filtered,
            };
        }

        var contentTerms = terms.Where(t => t.Type == SearchTermType.Content).ToList();
        if (contentTerms.Count > 0)
        {
            filtered = ApplyContentSearch(filtered, contentTerms);
        }

        return filtered.Take(100000).ToList();
    }

    private static IEnumerable<FileEntry> ApplySizeFilter(IEnumerable<FileEntry> entries, string value)
    {
        var match = Regex.Match(value, @"^([<>])?\s*(\d+(?:\.\d+)?)\s*(b|kb|mb|gb)?$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return entries;

        string op = match.Groups[1].Value;
        double number = double.Parse(match.Groups[2].Value);
        string unit = match.Groups[3].Value.ToLowerInvariant();

        long bytes = unit switch
        {
            "kb" => (long)(number * 1024),
            "mb" => (long)(number * 1024 * 1024),
            "gb" => (long)(number * 1024 * 1024 * 1024),
            _ => (long)number,
        };

        return op switch
        {
            ">" => entries.Where(e => !e.IsDirectory && e.Size > bytes),
            "<" => entries.Where(e => !e.IsDirectory && e.Size < bytes),
            _ => entries.Where(e => !e.IsDirectory && Math.Abs(e.Size - bytes) < bytes * 0.05),
        };
    }

    private static IEnumerable<FileEntry> ApplyDateFilter(
        IEnumerable<FileEntry> entries, string value,
        Func<FileEntry, DateTime?> dateSelector)
    {
        if (value.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            var today = DateTime.UtcNow.Date;
            return entries.Where(e =>
            {
                var d = dateSelector(e);
                return d.HasValue && d.Value.Date == today;
            });
        }

        if (value.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            return entries.Where(e =>
            {
                var d = dateSelector(e);
                return d.HasValue && d.Value.Date == yesterday;
            });
        }

        var rangeMatch = Regex.Match(value,
            @"^(\d{4}(?:-\d{2}(?:-\d{2})?)?)\s*\.\.\s*(\d{4}(?:-\d{2}(?:-\d{2})?)?)?$");
        if (rangeMatch.Success)
        {
            string startStr = rangeMatch.Groups[1].Value;
            string endStr = rangeMatch.Groups[2].Value;

            DateTime? start = ParseDate(startStr);
            DateTime? end = ParseDate(endStr);

            return entries.Where(e =>
            {
                var d = dateSelector(e);
                if (!d.HasValue) return false;
                if (start.HasValue && d.Value < start.Value) return false;
                if (end.HasValue && d.Value > end.Value.AddDays(1)) return false;
                return true;
            });
        }

        return entries;
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        if (value.Length == 4 && int.TryParse(value, out int year))
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (DateTime.TryParse(value, out var dt))
            return dt.ToUniversalTime();

        return null;
    }

    private static IEnumerable<FileEntry> ApplyDriveFilter(IEnumerable<FileEntry> entries, string value)
    {
        var drives = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => char.ToUpperInvariant(d.Trim().Length > 0 ? d.Trim()[0] : '?'))
            .ToHashSet();

        return entries.Where(e => drives.Contains(e.DriveLetter));
    }

    private static IEnumerable<FileEntry> ApplyAttributeFilter(IEnumerable<FileEntry> entries, string value)
    {
        return value.ToLowerInvariant() switch
        {
            "hidden" => entries.Where(e => e.IsHidden),
            "readonly" => entries.Where(e => e.IsReadOnly),
            "system" => entries.Where(e => e.IsSystem),
            "directory" or "dir" => entries.Where(e => e.IsDirectory),
            "file" => entries.Where(e => !e.IsDirectory),
            "archive" => entries.Where(e => e.IsArchive),
            "temporary" => entries.Where(e => e.IsTemporary),
            _ => entries,
        };
    }

    private static IEnumerable<FileEntry> ApplyContentSearch(
        IEnumerable<FileEntry> entries, List<SearchTerm> contentTerms)
    {
        const long maxSize = 10 * 1024 * 1024;
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".rst", ".log", ".csv", ".tsv",
            ".xml", ".html", ".htm", ".xhtml", ".svg",
            ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".config",
            ".cs", ".csproj", ".sln",
            ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs",
            ".css", ".scss", ".sass", ".less",
            ".py", ".pyw", ".rb", ".pl", ".pm", ".php", ".java",
            ".cpp", ".c", ".h", ".hpp", ".hxx", ".cxx", ".cc",
            ".go", ".rs", ".swift", ".kt", ".kts", ".dart",
            ".sql", ".sh", ".bash", ".zsh", ".ps1", ".bat", ".cmd",
            ".dockerfile", ".env", ".gitignore", ".editorconfig",
            ".gradle", ".groovy", ".properties", ".rake", ".rabl",
            ".tex", ".bib",
            ".makefile", ".cmake", ".mk",
            ".sql", ".prql",
        };

        string? searchText = contentTerms[0].ContentValue;
        if (string.IsNullOrEmpty(searchText)) return entries;

        return entries.Where(e =>
        {
            if (e.IsDirectory) return false;
            if (e.Size > maxSize || e.Size == 0) return false;
            string ext = Path.GetExtension(e.FullPath);
            if (!textExtensions.Contains(ext)) return false;

            try
            {
                using var fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
                using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);

                int totalRead = 0;
                char[] buf = new char[4096];
                while (totalRead < maxSize)
                {
                    int read = sr.ReadBlock(buf, 0, buf.Length);
                    if (read == 0) break;
                    totalRead += read;
                    if (((ReadOnlySpan<char>)buf.AsSpan(0, read)).IndexOf(searchText.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }

            return false;
        });
    }
}

internal enum SearchTermType
{
    Name,
    Size,
    Modified,
    Created,
    Drive,
    Attribute,
    Content,
}

internal class SearchTerm
{
    public string Value { get; init; } = string.Empty;
    public bool IsNegation { get; init; }
    public bool IsQuoted { get; init; }
    public string? PathPrefix { get; init; }
    public string[]? OrValues { get; init; }
    public SearchTermType Type { get; init; }
    public string? ContentValue { get; init; }
}
