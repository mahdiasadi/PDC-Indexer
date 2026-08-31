using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Watchers;

public class FatWatcher : IVolumeWatcher, IDisposable
{
    private readonly char _driveLetter;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly HashSet<string> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    public char DriveLetter => _driveLetter;
    public bool IsRunning => _watcher != null;

    public event Action<FileChangeEvent>? ChangeDetected;

    public FatWatcher(char driveLetter)
    {
        _driveLetter = char.ToUpperInvariant(driveLetter);
    }

    public void Start()
    {
        string root = $@"{_driveLetter}:\";

        _watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = 65536,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.CreationTime,
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        _debounceTimer = new Timer(DebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _pendingChanges.Add(e.FullPath);
            _debounceTimer?.Change(500, Timeout.Infinite);
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        EmitChange(new FileChangeEvent
        {
            Type = ChangeType.Deleted,
            FullPath = e.FullPath,
            DriveLetter = _driveLetter,
            IsDirectory = Directory.Exists(e.FullPath),
        });
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        EmitChange(new FileChangeEvent
        {
            Type = ChangeType.Renamed,
            FullPath = e.FullPath,
            OldPath = e.OldFullPath,
            DriveLetter = _driveLetter,
            IsDirectory = Directory.Exists(e.FullPath),
        });
    }

    private void DebounceElapsed(object? state)
    {
        string[] paths;
        lock (_lock)
        {
            paths = [.. _pendingChanges];
            _pendingChanges.Clear();
        }

        foreach (string path in paths)
        {
            try
            {
                var attrs = File.GetAttributes(path);
                bool isDir = (attrs & FileAttributes.Directory) != 0;
                var info = isDir ? null : new FileInfo(path);

                EmitChange(new FileChangeEvent
                {
                    Type = ChangeType.Created,
                    FullPath = path,
                    DriveLetter = _driveLetter,
                    IsDirectory = isDir,
                    Size = info?.Length ?? 0,
                });
            }
            catch
            {
                EmitChange(new FileChangeEvent
                {
                    Type = ChangeType.Modified,
                    FullPath = path,
                    DriveLetter = _driveLetter,
                });
            }
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Stop();
        try { Start(); } catch { }
    }

    private void EmitChange(FileChangeEvent evt)
    {
        ChangeDetected?.Invoke(evt);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
