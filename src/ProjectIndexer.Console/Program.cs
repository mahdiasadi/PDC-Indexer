using System.Diagnostics;
using ProjectIndexer.Core;
using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;



Console.WriteLine("=== ENGINE PIPELINE BENCH (C:) ===");

var dbPath = Path.Combine(Path.GetTempPath(), "engbench_" + Guid.NewGuid().ToString("N"));
var db = new IndexDatabase(dbPath);
var provider = new MftIndexer('C');
var engine = new IndexEngine(provider, db);

var sw = Stopwatch.StartNew();
Console.WriteLine("BuildIndex start");
var entries = engine.BuildIndex(new ProgressLogger());
Console.WriteLine($"BuildIndex done: {entries.Count} entries in {sw.Elapsed.TotalSeconds:F1}s");

sw.Restart();
Console.WriteLine("SaveToDatabase start");
engine.SaveToDatabase();
Console.WriteLine($"SaveToDatabase done in {sw.Elapsed.TotalSeconds:F1}s");

sw.Restart();
Console.WriteLine("Search 'notepad' start");
var r = engine.Search("notepad");
Console.WriteLine($"Search 'notepad' returned {r.Count} in {sw.ElapsedMilliseconds}ms");

try { Directory.Delete(dbPath, true); } catch { }
Console.WriteLine("ALL DONE");
class ProgressLogger : IProgress<IndexProgress>
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public void Report(IndexProgress value)
    {
        Console.WriteLine($"  [{_sw.Elapsed.TotalSeconds:F1}s] {value}");
    }
}