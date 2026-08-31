using System.Diagnostics;
using ProjectIndexer.Core;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== ProjectIndexer — Phase 2: All Providers ===");
Console.WriteLine();

bool isAdmin = MftIndexer.IsAdministrator();
Console.WriteLine($"Administrator: {isAdmin}");
Console.WriteLine();

var drives = FileSystemFactory.GetIndexableDrives();
Console.WriteLine($"Indexable drives found: {drives.Count}");
foreach (var d in drives)
{
    var type = FileSystemFactory.DetectFileSystemType(d);
    Console.WriteLine($"  {d}:\\  [{type}]");
}

Console.WriteLine();
Console.Write("Select drive to index (or press Enter to skip): ");
string? input = Console.ReadLine();
if (string.IsNullOrWhiteSpace(input)) return;

char selectedDrive = char.ToUpperInvariant(input.Trim()[0]);
Console.WriteLine($"\nIndexing drive {selectedDrive}:\\ ...");

try
{
    var provider = FileSystemFactory.CreateProvider(selectedDrive);
    Console.WriteLine($"Provider: {provider.GetType().Name} ({provider.FileSystemType})");

    if (provider.FileSystemType == FileSystemType.Ntfs && !isAdmin)
    {
        Console.WriteLine("ERROR: NTFS indexing requires administrator privileges.");
        Console.WriteLine("Run as Administrator and try again.");
        return;
    }

    long startTime = Stopwatch.GetTimestamp();
    double lastMark = 0;
    bool bfsStarted = false;
    var progress = new Progress<IndexProgress>(p =>
    {
        if (p.Stage == IndexStage.ParsingRecords && p.TotalRecords > 0)
        {
            double pct = p.ParsedRecords * 100.0 / p.TotalRecords;
            if (pct - lastMark >= 5.0)
            {
                lastMark = pct;
                double elapsed = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
                Console.WriteLine($"  [{pct:F0}%] {p.ParsedRecords:N0}/{p.TotalRecords:N0} records, {elapsed:F0}ms");
            }
        }
        else if (p.Stage == IndexStage.ReconstructingPaths)
        {
            if (!bfsStarted)
            {
                bfsStarted = true;
                Console.WriteLine($"  BFS path reconstruction started at {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds:F0}ms");
            }
            if (p.ParsedRecords > 0 && p.ParsedRecords % 100000 == 0)
                Console.WriteLine($"  BFS: {p.ParsedRecords:N0} entries at {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds:F0}ms");
        }
        else if (p.Stage == IndexStage.Completed)
        {
            Console.WriteLine($"  Done at {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds:F0}ms");
        }
    });

    var entries = provider.EnumerateFiles(progress);
    long elapsed = Stopwatch.GetElapsedTime(startTime).Milliseconds;

    Console.WriteLine();
    Console.WriteLine($"=== Results for {selectedDrive}:\\ ===");
    Console.WriteLine($"Provider:      {provider.GetType().Name} ({provider.FileSystemType})");
    Console.WriteLine($"Total entries: {entries.Count:N0}");
    Console.WriteLine($"Files:         {entries.Count(e => !e.IsDirectory):N0}");
    Console.WriteLine($"Directories:   {entries.Count(e => e.IsDirectory):N0}");
    Console.WriteLine($"Time:          {elapsed} ms");
    Console.WriteLine($"Speed:         {entries.Count / Math.Max(1, elapsed / 1000.0):N0} entries/sec");

    Console.WriteLine();
    Console.WriteLine("=== Sample Entries (first 15) ===");
    Console.WriteLine($"{"Name",-40} {"Size",-12} {"Type",-8} {"Path"}");
    Console.WriteLine(new string('-', 120));

    foreach (var entry in entries.Take(15))
    {
        string name = entry.Name.Length > 38 ? entry.Name[..35] + "..." : entry.Name;
        string size = entry.IsDirectory ? "<DIR>" : FormatSize(entry.Size);
        Console.WriteLine($"{name,-40} {size,-12} {(entry.IsDirectory ? "DIR" : "FILE"),-8} {entry.FullPath}");
    }

    if (entries.Count > 15)
        Console.WriteLine($"... and {entries.Count - 15:N0} more entries");
}
catch (NotSupportedException ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"UNEXPECTED ERROR: {ex.GetType().Name}: {ex.Message}");
}

static string FormatSize(long bytes)
{
    if (bytes < 1024) return $"{bytes} B";
    if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
    if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
    return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
}
