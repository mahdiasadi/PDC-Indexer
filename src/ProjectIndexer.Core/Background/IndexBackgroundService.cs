using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectIndexer.Core.Archiving;
using ProjectIndexer.Core.Configuration;
using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Watchers;

namespace ProjectIndexer.Core.Background;

public class IndexBackgroundService : BackgroundService
{
    private readonly ILogger<IndexBackgroundService> _logger;
    private readonly Dictionary<char, IVolumeWatcher> _watchers = new();
    private readonly Dictionary<char, IndexEngine> _engines = new();
    private readonly IndexDatabase _database;
    private readonly ArchiveManager _archiveManager;

    public IReadOnlyDictionary<char, IVolumeWatcher> Watchers => _watchers;
    public IReadOnlyDictionary<char, IndexEngine> Engines => _engines;

    public IndexBackgroundService(ILogger<IndexBackgroundService> logger, IOptions<DatabaseOptions>? dbOptions = null, ArchiveManager? archiveManager = null)
    {
        _logger = logger;
        _archiveManager = archiveManager ?? new ArchiveManager();

        string? folder = dbOptions?.Value.DatabaseFolder;
        _database = string.IsNullOrEmpty(folder) ? new IndexDatabase() : new IndexDatabase(folder);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IndexBackgroundService starting");

        try
        {
            InitializeWatchers(stoppingToken);
            _logger.LogInformation("Watching {Count} drives", _watchers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize watchers");
        }

        try
        {
            await Task.Delay(-1, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        StopWatchers();
        _logger.LogInformation("IndexBackgroundService stopped");
    }

    private void InitializeWatchers(CancellationToken ct)
    {
        var drives = FileSystemFactory.GetIndexableDrives();

        foreach (char drive in drives)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var engine = new IndexEngine(
                    FileSystemFactory.CreateProvider(drive),
                    _database);

                bool loaded = engine.LoadFromDatabase();
                if (loaded)
                {
                    _logger.LogInformation("Loaded cached index for {Drive}: ({Count} entries)",
                        drive, engine.EntryCount);
                }

                var watcher = VolumeWatcherFactory.Create(drive, loaded ? engine.MemoryIndex.Entries : null);
                watcher.ChangeDetected += evt => HandleChange(engine, evt);
                watcher.Start();

                _watchers[drive] = watcher;
                _engines[drive] = engine;
                _logger.LogInformation("Watching {Drive}: ({Type})", drive,
                    watcher is UsnVolumeWatcher ? "USN Journal" : "FileSystemWatcher");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize watcher for {Drive}:", drive);
            }
        }
    }

    private void HandleChange(IndexEngine engine, FileChangeEvent evt)
    {
        try
        {
            var index = engine.MemoryIndex;

            switch (evt.Type)
            {
                case ChangeType.Created:
                    var newEntry = new FileEntry
                    {
                        Frn = evt.Frn,
                        Name = Path.GetFileName(evt.FullPath),
                        FullPath = evt.FullPath,
                        DriveLetter = evt.DriveLetter,
                        IsDirectory = evt.IsDirectory,
                        Size = evt.Size,
                    };
                    index.Add(newEntry);
                    break;

                case ChangeType.Modified:
                    var existing = index.GetByPath(evt.FullPath);
                    if (existing != null)
                    {
                        existing.Size = evt.Size > 0 ? evt.Size : existing.Size;
                    }
                    break;

                case ChangeType.Deleted:
                    var deleted = index.GetByPath(evt.FullPath);
                    if (deleted != null)
                    {
                        RemoveFromIndex(index, deleted);
                    }
                    break;

                case ChangeType.Renamed:
                    var renamed = index.GetByPath(evt.OldPath ?? "");
                    if (renamed != null)
                    {
                        renamed.Name = Path.GetFileName(evt.FullPath);
                        renamed.FullPath = evt.FullPath;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling change event for {Path}", evt.FullPath);
        }
    }

    private static void RemoveFromIndex(InMemoryIndex index, FileEntry entry)
    {
        if (entry.IsDirectory)
        {
            var children = index.Filter(e =>
                e.FullPath.StartsWith(entry.FullPath + "\\", StringComparison.OrdinalIgnoreCase) ||
                e.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase));
            foreach (var child in children.ToList())
            {
                if (child.FullPath.Equals(entry.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    var exactMatch = index.GetByPath(child.FullPath);
                    if (exactMatch != null)
                    {
                        var entries = index.SearchByName(child.Name);
                        var toRemove = entries.FirstOrDefault(e => e.FullPath == child.FullPath);
                        if (toRemove != null)
                        {
                            var allEntries = index.Entries.ToList();
                            allEntries.Remove(toRemove);
                            index.Clear();
                            index.AddRange(allEntries);
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            var allEntries = index.Entries.ToList();
            allEntries.Remove(entry);
            index.Clear();
            index.AddRange(allEntries);
        }
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers.Values)
        {
            try { watcher.Stop(); } catch { }
        }
        _watchers.Clear();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IndexBackgroundService stopping");
        return base.StopAsync(cancellationToken);
    }
}
