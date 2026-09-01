using Microsoft.Win32.SafeHandles;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;

namespace ProjectIndexer.Core.Watchers;

public class UsnVolumeWatcher : IVolumeWatcher, IDisposable
{
    private readonly char _driveLetter;
    private readonly Dictionary<ulong, string> _frnPathCache = new();
    private SafeFileHandle? _volumeHandle;
    private long _usnJournalId;
    private long _nextUsn;
    private Timer? _pollTimer;
    private bool _disposed;

    public char DriveLetter => _driveLetter;
    public bool IsRunning => _pollTimer != null;

    public event Action<FileChangeEvent>? ChangeDetected;

    public UsnVolumeWatcher(char driveLetter, IEnumerable<FileEntry>? existingEntries = null)
    {
        _driveLetter = char.ToUpperInvariant(driveLetter);

        if (existingEntries != null)
        {
            foreach (var entry in existingEntries)
            {
                if (entry.Frn != 0 && !string.IsNullOrEmpty(entry.FullPath))
                    _frnPathCache[entry.Frn] = entry.FullPath;
            }
        }
    }

    public void Start()
    {
        string volumePath = $@"\\.\{_driveLetter}:";
        _volumeHandle = Win32Native.CreateFile(
            volumePath,
            Win32Native.GENERIC_READ,
            Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Win32Native.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (_volumeHandle.IsInvalid)
            throw new UnauthorizedAccessException(
                $"Cannot open volume {_driveLetter}:. Administrator rights required for USN Journal.");

        try
        {
            var journalData = UsnJournal.QueryJournal(_volumeHandle);
            _usnJournalId = journalData.UsnJournalId;
            _nextUsn = journalData.NextUsn;
        }
        catch
        {
            try
            {
                UsnJournal.CreateJournal(_volumeHandle);
                var journalData = UsnJournal.QueryJournal(_volumeHandle);
                _usnJournalId = journalData.UsnJournalId;
                _nextUsn = journalData.NextUsn;
            }
            catch (Exception ex)
            {
                _volumeHandle.Dispose();
                _volumeHandle = null;
                throw new InvalidOperationException(
                    $"Failed to create USN journal on {_driveLetter}: {ex.Message}", ex);
            }
        }

        _pollTimer = new Timer(PollJournal, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _volumeHandle?.Dispose();
        _volumeHandle = null;
    }

    private void PollJournal(object? state)
    {
        if (_volumeHandle == null || _volumeHandle.IsInvalid) return;

        try
        {
            var records = UsnJournal.ReadJournalRecords(_volumeHandle, _nextUsn, _usnJournalId);
            if (records.Count == 0) return;

            long maxUsn = _nextUsn;
            var pendingRenames = new List<(UsnRecord Record, bool IsOld)>();

            foreach (var record in records)
            {
                if (record.Usn > maxUsn) maxUsn = record.Usn;

                if (IsCreate(record))
                {
                    string? path = BuildPath(record, isDeleted: false);
                    if (path != null)
                    {
                        _frnPathCache[(ulong)record.FileReferenceNumber] = path;
                        EmitChange(new FileChangeEvent
                        {
                            Type = ChangeType.Created,
                            FullPath = path,
                            DriveLetter = _driveLetter,
                            IsDirectory = record.IsDirectory,
                            Frn = (ulong)record.FileReferenceNumber,
                        });
                    }
                }

                if (IsDelete(record))
                {
                    string? path = ResolvePath((ulong)record.FileReferenceNumber);
                    if (path == null) path = BuildPathForDelete(record);
                    if (path != null)
                    {
                        _frnPathCache.Remove((ulong)record.FileReferenceNumber);
                        EmitChange(new FileChangeEvent
                        {
                            Type = ChangeType.Deleted,
                            FullPath = path,
                            DriveLetter = _driveLetter,
                            IsDirectory = record.IsDirectory,
                            Frn = (ulong)record.FileReferenceNumber,
                        });
                    }
                }

                if ((record.Reason & UsnJournal.USN_REASON_RENAME_OLD_NAME) != 0)
                    pendingRenames.Add((record, true));

                if ((record.Reason & UsnJournal.USN_REASON_RENAME_NEW_NAME) != 0)
                    pendingRenames.Add((record, false));

                if (IsModify(record))
                {
                    string? path = ResolvePath((ulong)record.FileReferenceNumber);
                    if (path == null) path = BuildPath(record, false);
                    if (path != null)
                    {
                        EmitChange(new FileChangeEvent
                        {
                            Type = ChangeType.Modified,
                            FullPath = path,
                            DriveLetter = _driveLetter,
                            IsDirectory = record.IsDirectory,
                            Size = 0,
                            Frn = (ulong)record.FileReferenceNumber,
                        });
                    }
                }
            }

            ProcessRenames(pendingRenames);

            _nextUsn = maxUsn;
        }
        catch
        {
        }
    }

    private void ProcessRenames(List<(UsnRecord Record, bool IsOld)> pendingRenames)
    {
        for (int i = 0; i < pendingRenames.Count; i++)
        {
            if (!pendingRenames[i].IsOld) continue;

            string oldName = pendingRenames[i].Record.FileName;
            ulong frn = (ulong)pendingRenames[i].Record.FileReferenceNumber;

            for (int j = i + 1; j < pendingRenames.Count; j++)
            {
                if (pendingRenames[j].IsOld) continue;
                if ((ulong)pendingRenames[j].Record.FileReferenceNumber != frn) continue;

                string? oldPath = ResolvePath(frn);
                if (oldPath == null)
                {
                    oldPath = BuildPathForDelete(pendingRenames[i].Record);
                }

                string newName = pendingRenames[j].Record.FileName;
                string? parentPath = GetParentPath((ulong)pendingRenames[j].Record.ParentFileReferenceNumber);
                string? newPath = parentPath != null ? $@"{parentPath}\{newName}" : $@"{_driveLetter}:\{newName}";

                if (oldPath != null && newPath != null)
                {
                    _frnPathCache[frn] = newPath;
                    EmitChange(new FileChangeEvent
                    {
                        Type = ChangeType.Renamed,
                        FullPath = newPath,
                        OldPath = oldPath,
                        DriveLetter = _driveLetter,
                        IsDirectory = pendingRenames[j].Record.IsDirectory,
                        Frn = frn,
                    });
                }

                break;
            }
        }
    }

    private string? BuildPath(UsnRecord record, bool isDeleted)
    {
        string? parentPath = GetParentPath((ulong)record.ParentFileReferenceNumber);
        if (parentPath != null)
            return $@"{parentPath}\{record.FileName}";

        if (!isDeleted)
            _frnPathCache[(ulong)record.ParentFileReferenceNumber] = $@"{_driveLetter}:\";

        return $@"{_driveLetter}:\{record.FileName}";
    }

    private string? BuildPathForDelete(UsnRecord record)
    {
        string? parentPath = GetParentPath((ulong)record.ParentFileReferenceNumber);
        if (parentPath != null)
            return $@"{parentPath}\{record.FileName}";
        return $@"{_driveLetter}:\{record.FileName}";
    }

    private string? GetParentPath(ulong parentFrn)
    {
        if (_frnPathCache.TryGetValue(parentFrn, out var path))
            return path;
        return null;
    }

    private string? ResolvePath(ulong frn)
    {
        if (_frnPathCache.TryGetValue(frn, out var path))
            return path;
        return null;
    }

    private void UpdatePathCache(ulong frn, string path)
    {
        _frnPathCache[frn] = path;
    }

    private static bool IsCreate(UsnRecord r) =>
        (r.Reason & UsnJournal.USN_REASON_FILE_CREATE) != 0;

    private static bool IsDelete(UsnRecord r) =>
        (r.Reason & (UsnJournal.USN_REASON_FILE_DELETE | UsnJournal.USN_REASON_EXTEND_FILE_DELETE)) != 0;

    private static bool IsModify(UsnRecord r) =>
        (r.Reason & (UsnJournal.USN_REASON_DATA_OVERWRITE |
                     UsnJournal.USN_REASON_DATA_EXTEND |
                     UsnJournal.USN_REASON_DATA_TRUNCATION)) != 0 &&
        (r.Reason & UsnJournal.USN_REASON_FILE_CREATE) == 0 &&
        (r.Reason & UsnJournal.USN_REASON_RENAME_NEW_NAME) == 0;

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
