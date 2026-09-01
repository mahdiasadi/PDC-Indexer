using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectIndexer.Core;
using ProjectIndexer.Core.Archiving;
using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.FileSystem;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Wpf.Collections;

namespace ProjectIndexer.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<char, IndexEngine> _engines = [];
    private readonly ArchiveManager _archiveManager;
    private readonly string _databaseFolder;
    private readonly IndexDatabase _sharedDatabase;
    private readonly List<FileEntry> _loadedArchiveEntries = [];
    private CancellationTokenSource? _indexCts;
    private readonly List<FileEntry> _pendingResults = [];
    private readonly object _pendingLock = new();
    private System.Threading.Timer? _uiUpdateTimer;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isIndexing;

    [ObservableProperty]
    private double _indexProgress;

    [ObservableProperty]
    private string _indexProgressText = "";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _totalDirectories;

    [ObservableProperty]
    private int _archiveCount;

    [ObservableProperty]
    private long _totalArchiveSize;

    [ObservableProperty]
    private string _folderPath = "";

    [ObservableProperty]
    private RangeObservableCollection<FileEntryViewModel> _results = new();

    [ObservableProperty]
    private FileEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private ObservableCollection<DriveViewModel> _drives = [];

    [ObservableProperty]
    private ObservableCollection<DriveInfoViewModel> _allDrives = [];

    public ICollectionView ResultsView { get; }

    public MainViewModel()
    {
        _databaseFolder = ReadDatabaseFolderFromConfig();
        _sharedDatabase = new IndexDatabase(_databaseFolder);
        _archiveManager = new ArchiveManager();
        ResultsView = CollectionViewSource.GetDefaultView(Results);

        try { LoadDrives(); } catch { StatusText = "Error loading drives"; }
        try { RefreshArchiveInfo(); } catch { }
        
        // Load indexes in background
        _ = Task.Run(LoadIndexesAsync);
    }

    private static string ReadDatabaseFolderFromConfig()
    {
        try
        {
            string json = File.ReadAllText("appsettings.json");
            var doc = JsonDocument.Parse(json);
            var folder = doc.RootElement.GetProperty("DatabaseSettings").GetProperty("DatabaseFolder").GetString();
            if (!string.IsNullOrEmpty(folder))
            {
                folder = Environment.ExpandEnvironmentVariables(folder);
                try
                {
                    Directory.CreateDirectory(folder);
                    return folder;
                }
                catch
                {
                    // Fall back if path is invalid (e.g., drive doesn't exist)
                }
            }
        }
        catch { }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectIndexer");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private void EnsureTimer()
    {
        if (_uiUpdateTimer == null)
            _uiUpdateTimer = new System.Threading.Timer(_ => FlushPendingResults(), null, 500, 500);
    }

    public void Shutdown()
    {
        _uiUpdateTimer?.Dispose();
        _uiUpdateTimer = null;
        _sharedDatabase?.Dispose();
    }

    private async Task LoadIndexesAsync()
    {
        try
        {
            await Task.Delay(500); // Let UI initialize first
            
            foreach (var drive in Drives)
            {
                try
                {
                    var provider = FileSystemFactory.CreateProvider(drive.DriveLetter);
                    var engine = new IndexEngine(provider, _sharedDatabase);

                    if (engine.LoadFromFastIndex())
                    {
                        _engines[drive.DriveLetter] = engine;
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            StatusText = $"Loaded fast index for {drive.DriveLetter}:\\ ({engine.EntryCount:N0} entries)";
                        });
                    }
                }
                catch { }
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalFiles = _engines.Values.Sum(e => e.EntryCount);
                TotalDirectories = _engines.Values.Sum(e => e.MemoryIndex.Filter(f => f.IsDirectory).Count());
                RefreshArchiveInfo();

                if (string.IsNullOrWhiteSpace(SearchText))
                    ShowAllIndexed();
                else
                    ExecuteSearch(SearchText);
            });
        }
        catch { }
    }

    private void FlushPendingResults()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            lock (_pendingLock) _pendingResults.Clear();
            return;
        }

        List<FileEntry> batch;
        lock (_pendingLock)
        {
            if (_pendingResults.Count == 0) return;
            batch = [.. _pendingResults];
            _pendingResults.Clear();
        }

        try
        {
            if (System.Windows.Application.Current?.Dispatcher == null) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (Results.Count >= 100000) return;
                    int remaining = 100000 - Results.Count;

                    var items = new List<FileEntryViewModel>(Math.Min(remaining, batch.Count));
                    foreach (var entry in batch.Take(remaining))
                        items.Add(FileEntryViewModel.FromEntry(entry));

                    Results.AddRange(items);
                }
                catch { }
            });
        }
        catch { }
    }

    private void LoadDrives()
    {
        Drives.Clear();
        AllDrives.Clear();

        var allDrives = FileSystemFactory.GetAllDrives();
        foreach (var (letter, fs, ready, total, free) in allDrives)
        {
            var type = FileSystemFactory.DetectFileSystemType(letter);
            bool isIndexable = ready && (type != FileSystemType.Unknown);
            
            Drives.Add(new DriveViewModel
            {
                DriveLetter = letter,
                FileSystemType = type,
                IsAdminRequired = type == FileSystemType.Ntfs,
                IsAdmin = MftIndexer.IsAdministrator(),
                IsSelected = isIndexable,
            });

            AllDrives.Add(new DriveInfoViewModel
            {
                DriveLetter = letter,
                FileSystem = fs,
                IsReady = ready,
                TotalSize = total,
                FreeSpace = free,
                IsIndexable = isIndexable,
                RequiresAdmin = type == FileSystemType.Ntfs,
            });
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounce search - wait 300ms after last keystroke
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Threading.Timer(async _ =>
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ExecuteSearch(value);
            });
        }, null, 300, -1);
    }

    private System.Threading.Timer? _searchDebounceTimer;

    private void ExecuteSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowAllIndexed();
            return;
        }

        // Allow searching partially built indexes while a drive is being indexed.
        if (!_engines.Values.Any(e => e.IsIndexed || e.EntryCount > 0))
        {
            StatusText = "No indexes loaded yet. Click 'Load From Database' or 'Index All Drives' first.";
            return;
        }

        lock (_pendingLock)
            _pendingResults.Clear();

        var sw = Stopwatch.StartNew();
        var allResults = new List<FileEntry>();

        int engineCount = 0, archiveCount = 0;
        foreach (var (_, engine) in _engines)
        {
            if (engine.EntryCount == 0) continue;
            var r = engine.Search(query);
            engineCount += r.Count;
            allResults.AddRange(r);
        }

        var archiveResults = _archiveManager.SearchAllArchives(query);
        archiveCount = archiveResults.Count;
        foreach (var (entry, _, _) in archiveResults)
        {
            if (!allResults.Any(e => e.FullPath == entry.FullPath))
                allResults.Add(entry);
        }

        var final = allResults.DistinctBy(e => e.FullPath).Take(100000).ToList();

        var vms = new List<FileEntryViewModel>(final.Count);
        foreach (var entry in final)
            vms.Add(FileEntryViewModel.FromEntry(entry));
        Results.ReplaceAll(vms);

        sw.Stop();
        var sample = final.Take(5).Select(e => e.Name).ToList();
        StatusText = $"query='{query}' | {final.Count:N0} results ({engineCount} eng + {archiveCount} arc) in {sw.ElapsedMilliseconds}ms | samples: {string.Join(", ", sample)}";
    }

    [RelayCommand]
    private async Task IndexAllDrives()
    {
        if (IsIndexing) return;

        _indexCts = new CancellationTokenSource();
        IsIndexing = true;
        EnsureTimer();

        try
        {
            foreach (var drive in Drives.Where(d => d.IsSelected))
            {
                if (_indexCts.Token.IsCancellationRequested) break;

                try
                {
                    var provider = FileSystemFactory.CreateProvider(drive.DriveLetter);

                    bool isNewEngine = !_engines.TryGetValue(drive.DriveLetter, out var engine);
                    if (isNewEngine)
                        engine = new IndexEngine(provider, new Core.Database.IndexDatabase(_databaseFolder));

                    engine!.OnEntryIndexed = entry =>
                    {
                        lock (_pendingLock)
                            _pendingResults.Add(entry);
                    };

                    // Background task for periodic archive save during long indexing.
                    // Uses its own token so cancelling it after a drive completes
                    // does not abort indexing of subsequent drives.
                    var archiveCts = new CancellationTokenSource();
                    var archiveSaveTask = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), archiveCts.Token);
                            while (!archiveCts.Token.IsCancellationRequested)
                            {
                                if (engine.IsIndexed && engine.MemoryIndex.Entries.Count > 0)
                                {
                                    await Task.Run(() =>
                                    {
                                        engine.SaveToDatabase();
                                        var archivePath = _archiveManager.CreateArchive(
                                            drive.DriveLetter, engine.MemoryIndex.Entries);
                                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            StatusText = $"Auto-saved archive: {Path.GetFileName(archivePath)} ({engine.EntryCount:N0} entries)";
                                        });
                                    }, archiveCts.Token);
                                }
                                await Task.Delay(TimeSpan.FromMinutes(2), archiveCts.Token);
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch { }
                    }, archiveCts.Token);

                    var progress = new Progress<IndexProgress>(p =>
                    {
                        double percent = p.Stage switch
                        {
                            IndexStage.ParsingRecords when p.TotalRecords > 0 => 5 + p.ParsedRecords * 55.0 / p.TotalRecords,
                            IndexStage.ReconstructingPaths when p.TotalRecords > 0 => 60 + p.ParsedRecords * 30.0 / p.TotalRecords,
                            IndexStage.Completed => 100,
                            IndexStage.Failed => 100,
                            _ => 5,
                        };
                        IndexProgress = Math.Min(100, percent) / 100.0;
                        IndexProgressText = p.ToString();
                        StatusText = $"Indexing {drive.DriveLetter}:\\ — {p}";
                    });

                    var indexFolder = string.IsNullOrWhiteSpace(FolderPath) ? null : FolderPath;
                    await Task.Run(() => engine.BuildIndex(progress, indexFolder), _indexCts.Token);

                    archiveCts.Cancel(); // Stop periodic saves
                    try { await archiveSaveTask; } catch { }
                    archiveCts.Dispose();

                    if (isNewEngine)
                        _engines[drive.DriveLetter] = engine;

                    TotalFiles = _engines.Values.Sum(e => e.EntryCount);
                    TotalDirectories = _engines.Values.Sum(e => e.MemoryIndex.Filter(f => f.IsDirectory).Count());
                    StatusText = $"Indexed {drive.DriveLetter}:\\ — {engine.EntryCount:N0} entries, saving database & archive...";

                    var archiveProgress = new Progress<string>(msg =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => StatusText = msg);
                    });

                    await Task.Run(() =>
                    {
                        engine.SaveToDatabase();
                        var archivePath = _archiveManager.CreateArchive(
                            drive.DriveLetter, engine.MemoryIndex.Entries, null, archiveProgress);
                    });

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "Archive creation completed";
                    });

                    RefreshArchiveInfo();
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (string.IsNullOrWhiteSpace(SearchText))
                            ShowAllIndexed();
                        else
                            ExecuteSearch(SearchText);
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    StatusText = $"Admin required for {drive.DriveLetter}:\\ — skipping";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error indexing {drive.DriveLetter}:\\: {ex.Message}";
                }
            }
        }
        finally
        {
            IsIndexing = false;
            IndexProgress = 0;
            RefreshArchiveInfo();
        }
    }

    [RelayCommand]
    private void ClearFolder()
    {
        FolderPath = "";
    }

    [RelayCommand]
    private void CancelIndexing()
    {
        _indexCts?.Cancel();
        StatusText = "Indexing cancelled";
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (SelectedEntry == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedEntry.FullPath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenPath()
    {
        if (SelectedEntry == null) return;
        try
        {
            string folder = System.IO.Path.GetDirectoryName(SelectedEntry.FullPath) ?? SelectedEntry.FullPath;
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedEntry == null) return;
        try
        {
            System.Windows.Clipboard.SetText(SelectedEntry.FullPath);
            StatusText = "Path copied to clipboard";
        }
        catch { }
    }

    [RelayCommand]
    private void CopyName()
    {
        if (SelectedEntry == null) return;
        try
        {
            System.Windows.Clipboard.SetText(SelectedEntry.Name);
            StatusText = "Name copied to clipboard";
        }
        catch { }
    }

    [RelayCommand]
    private async Task MigrateToFastIndex()
    {
        if (IsIndexing) return;

        _indexCts = new CancellationTokenSource();
        IsIndexing = true;
        StatusText = "Migrating SQLite indexes to fast format...";

        try
        {
            await Task.Run(() =>
            {
                FastIndexMigrator.MigrateAllDrives(_databaseFolder);
                FastIndexMigrator.MigrateArchives();
            }, _indexCts.Token);

            StatusText = "Migration completed. Restart app to use fast indexes.";
        }
        catch (Exception ex)
        {
            StatusText = $"Migration failed: {ex.Message}";
        }
        finally
        {
            IsIndexing = false;
        }
    }

    [RelayCommand]
    private async Task LoadFromDatabase()
    {
        if (IsIndexing) return;
        
        IsIndexing = true;
        StatusText = "Loading indexes...";

        await Task.Run(() =>
        {
            foreach (var drive in Drives)
            {
                try
                {
                    var provider = FileSystemFactory.CreateProvider(drive.DriveLetter);
                    var engine = new IndexEngine(provider, _sharedDatabase);

                    if (engine.LoadFromFastIndex())
                    {
                        _engines[drive.DriveLetter] = engine;
                    }
                    else if (engine.LoadFromDatabase())
                    {
                        _engines[drive.DriveLetter] = engine;
                        engine.SaveToDatabase();
                    }
                }
                catch { }
            }
        });

        TotalFiles = _engines.Values.Sum(e => e.EntryCount);
        TotalDirectories = _engines.Values.Sum(e => e.MemoryIndex.Filter(f => f.IsDirectory).Count());
        RefreshArchiveInfo();

        if (string.IsNullOrWhiteSpace(SearchText))
            ShowAllIndexed();
        else
            ExecuteSearch(SearchText);

        IsIndexing = false;
        StatusText = $"Loaded {_engines.Count} drive(s) - {TotalFiles:N0} files, {TotalDirectories:N0} dirs";
    }

    private void ShowAllIndexed()
    {
        var items = new List<FileEntryViewModel>();
        foreach (var (_, engine) in _engines)
        {
            if (!engine.IsIndexed) continue;
            foreach (var entry in engine.MemoryIndex.Entries.Take(50000))
                items.Add(FileEntryViewModel.FromEntry(entry));
        }

        foreach (var entry in _loadedArchiveEntries.Take(50000))
        {
            var vm = FileEntryViewModel.FromEntry(entry);
            vm.Source = "Archive";
            items.Add(vm);
        }

        Results.ReplaceAll(items);

        StatusText = $"{TotalFiles:N0} files, {TotalDirectories:N0} dirs indexed | {_engines.Count} drive(s) loaded | {_loadedArchiveEntries.Count:N0} archive entries";
    }

    private void RefreshArchiveInfo()
    {
        ArchiveCount = _archiveManager.GetArchiveCount();
        TotalArchiveSize = _archiveManager.GetTotalArchiveSize();
    }

    [RelayCommand]
    private void ShowArchives()
    {
        var archives = _archiveManager.ListArchives();
        if (archives.Count == 0)
        {
            StatusText = "No archives found";
            return;
        }

        var dialog = new ArchivePickerDialog(archives);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.SelectedArchive != null)
        {
            LoadArchiveEntries(dialog.SelectedArchive);
        }
    }

    [RelayCommand]
    private void RefreshArchives()
    {
        RefreshArchiveInfo();
        StatusText = $"Archives: {ArchiveCount} | Total size: {FormatSize(TotalArchiveSize)}";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private void LoadArchiveEntries(ArchiveInfo archive)
    {
        try
        {
            var loadProgress = new Progress<string>(msg =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => StatusText = msg);
            });

            var entries = _archiveManager.LoadArchive(archive.FilePath, loadProgress);
            _loadedArchiveEntries.AddRange(entries);

            var vms = new List<FileEntryViewModel>(entries.Count);
            foreach (var entry in entries)
            {
                var vm = FileEntryViewModel.FromEntry(entry);
                vm.Source = $"Archive: {archive.DisplayName}";
                vms.Add(vm);
            }
            Results.ReplaceAll(vms);

            TotalFiles += entries.Count(e => !e.IsDirectory);
            TotalDirectories += entries.Count(e => e.IsDirectory);
            StatusText = $"Loaded {entries.Count:N0} entries from archive {archive.DisplayName} — completed";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading archive: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SearchArchives(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        var results = _archiveManager.SearchAllArchives(query);

        var vms = new List<FileEntryViewModel>(results.Count);
        foreach (var (entry, archiveName, info) in results)
        {
            var vm = FileEntryViewModel.FromEntry(entry);
            vm.Source = $"Archive: {info.DisplayName}";
            vms.Add(vm);
        }
        Results.ReplaceAll(vms);

        StatusText = $"{results.Count} results from archives";
    }
}

public partial class FileEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _fullPath = "";
    [ObservableProperty] private string _sizeDisplay = "";
    [ObservableProperty] private long _size;
    [ObservableProperty] private string _dateModified = "";
    [ObservableProperty] private string _dateCreated = "";
    [ObservableProperty] private string _type = "";
    [ObservableProperty] private string _extension = "";
    [ObservableProperty] private string _attributes = "";
    [ObservableProperty] private bool _isDirectory;
    [ObservableProperty] private string _source = "";

    public static FileEntryViewModel FromEntry(FileEntry entry)
    {
        return new FileEntryViewModel
        {
            Name = entry.Name,
            FullPath = entry.FullPath,
            Size = entry.Size,
            SizeDisplay = entry.IsDirectory ? "<DIR>" : FormatSize(entry.Size),
            DateModified = entry.LastModifiedTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
            DateCreated = entry.CreationTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
            Type = entry.IsDirectory ? "File Folder" : GetFileType(entry.Name),
            Extension = entry.NameExtension,
            IsDirectory = entry.IsDirectory,
            Attributes = GetAttributesString(entry),
            Source = entry.DriveLetter.ToString() + ":\\",
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetFileType(string name)
    {
        int dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1) return "File";
        string ext = name[(dot + 1)..].ToUpperInvariant();
        return ext switch
        {
            "TXT" => "Text Document",
            "EXE" => "Application",
            "DLL" => "Application Extension",
            "PDF" => "PDF Document",
            "DOC" or "DOCX" => "Word Document",
            "XLS" or "XLSX" => "Excel Workbook",
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" => "Image",
            "ZIP" or "RAR" or "7Z" => "Archive",
            "MP3" or "WAV" or "FLAC" => "Audio",
            "MP4" or "AVI" or "MKV" => "Video",
            _ => $"{ext} File",
        };
    }

    private static string GetAttributesString(FileEntry entry)
    {
        var attrs = new List<string>();
        if (entry.IsDirectory) attrs.Add("D");
        if (entry.IsHidden) attrs.Add("H");
        if (entry.IsSystem) attrs.Add("S");
        if (entry.IsReadOnly) attrs.Add("R");
        if (entry.IsArchive) attrs.Add("A");
        return attrs.Count > 0 ? string.Join(" ", attrs) : "";
    }
}

public partial class DriveViewModel : ObservableObject
{
    [ObservableProperty] private char _driveLetter;
    [ObservableProperty] private FileSystemType _fileSystemType;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isAdminRequired;
    [ObservableProperty] private bool _isAdmin;

    public string DisplayName => $"{DriveLetter}:\\ [{FileSystemType}]";
    public string Status => IsAdminRequired && !IsAdmin ? "(needs admin)" : "";
}

public partial class DriveInfoViewModel : ObservableObject
{
    [ObservableProperty] private char _driveLetter;
    [ObservableProperty] private string _fileSystem = "";
    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private long _totalSize;
    [ObservableProperty] private long _freeSpace;
    [ObservableProperty] private bool _isIndexable;
    [ObservableProperty] private bool _requiresAdmin;

    public string DisplayName => $"{DriveLetter}:\\ ({FileSystem})";
    public string Status => !IsReady ? "Not Ready" : (RequiresAdmin ? "Needs Admin" : (IsIndexable ? "Indexable" : "Not Indexable"));
    public string SizeDisplay => IsReady ? $"{FormatSize(FreeSpace)} free of {FormatSize(TotalSize)}" : "";
    
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
