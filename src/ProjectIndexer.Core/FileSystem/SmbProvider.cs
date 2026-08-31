using System.Collections.Concurrent;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;

namespace ProjectIndexer.Core.FileSystem;

public class SmbProvider : IFileSystemProvider
{
    public FileSystemType FileSystemType => FileSystemType.Smb;
    public char DriveLetter { get; }
    public bool SupportsJournaling => false;
    public Action<FileEntry>? OnEntryIndexed { get; set; }

    private readonly string _uncPath;

    public SmbProvider(string uncPath)
    {
        if (string.IsNullOrWhiteSpace(uncPath))
            throw new ArgumentException("UNC path cannot be empty", nameof(uncPath));

        if (!uncPath.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("SMB path must start with \\\\ (UNC format)", nameof(uncPath));

        _uncPath = uncPath.TrimEnd('\\');
        DriveLetter = '?';
    }

    public bool CanProcess()
    {
        try
        {
            var dir = new DirectoryInfo(_uncPath);
            return dir.Exists;
        }
        catch
        {
            return false;
        }
    }

    public List<FileEntry> EnumerateFiles(IProgress<IndexProgress>? progress = null)
    {
        var progressInfo = new IndexProgress
        {
            DriveLetter = _uncPath,
            Stage = IndexStage.Starting
        };

        try
        {
            progressInfo.Stage = IndexStage.ReadingMft;
            progress?.Report(progressInfo);

            var entries = new ConcurrentBag<FileEntry>();
            var dirQueue = new ConcurrentQueue<string>();
            var processedDirs = new ConcurrentDictionary<string, byte>();
            long dirCount = 0;

            dirQueue.Enqueue(_uncPath);
            processedDirs.TryAdd(_uncPath.ToUpperInvariant(), 0);

            int workerCount = Math.Max(1, Environment.ProcessorCount / 2);
            using var completionEvent = new ManualResetEventSlim(false);
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        while (true)
                        {
                            if (exceptions.Count > 0) break;

                    if (dirQueue.TryDequeue(out string? currentDir))
                    {
                        ProcessSmbDirectory(currentDir, entries, dirQueue, processedDirs, OnEntryIndexed);
                                Interlocked.Increment(ref dirCount);

                                if (dirCount % 50 == 0)
                                {
                                    progressInfo.ParsedRecords = dirCount;
                                    progressInfo.FilesFound = entries.Count(e => !e.IsDirectory);
                                    progressInfo.DirectoriesFound = entries.Count(e => e.IsDirectory);
                                    progressInfo.CurrentPath = currentDir;
                                    progress?.Report(progressInfo);
                                }
                            }
                            else
                            {
                                if (dirQueue.IsEmpty)
                                    break;

                                Thread.SpinWait(50);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            Task.WaitAll(tasks);

            if (!exceptions.IsEmpty)
                throw new AggregateException("Errors during SMB enumeration", exceptions);

            progressInfo.Stage = IndexStage.Completed;
            progressInfo.ParsedRecords = dirCount;
            progressInfo.FilesFound = entries.Count(e => !e.IsDirectory);
            progressInfo.DirectoriesFound = entries.Count(e => e.IsDirectory);
            progress?.Report(progressInfo);

            return [.. entries];
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not AggregateException)
        {
            progressInfo.Stage = IndexStage.Failed;
            progress?.Report(progressInfo);
            throw new InvalidOperationException($"Failed to enumerate SMB share {_uncPath}: {ex.Message}", ex);
        }
    }

    private static void ProcessSmbDirectory(
        string directory,
        ConcurrentBag<FileEntry> entries,
        ConcurrentQueue<string> dirQueue,
        ConcurrentDictionary<string, byte> processedDirs,
        Action<FileEntry>? onEntryIndexed)
    {
        var handle = Win32Find.FindFirstFileW(directory + @"\*", out var findData);
        if (handle.IsInvalid)
            return;

        using (handle)
        {
            do
            {
                if (findData.IsSpecialEntry)
                    continue;

                string name = findData.cFileName;
                string fullPath = directory + @"\" + name;

                if (findData.IsDirectory)
                {
                    if (!findData.IsJunction && !findData.IsSymlink)
                    {
                        string upperPath = fullPath.ToUpperInvariant();
                        if (processedDirs.TryAdd(upperPath, 0))
                            dirQueue.Enqueue(fullPath);
                    }
                }

                var entry = new FileEntry
                {
                    Name = name,
                    FullPath = fullPath,
                    Size = findData.IsDirectory ? 0 : Math.Max(0, (long)findData.FileSize),
                    IsDirectory = findData.IsDirectory,
                    IsHidden = findData.IsHidden,
                    IsSystem = findData.IsSystem,
                    IsReadOnly = findData.IsReadOnly,
                    IsArchive = findData.IsArchive,
                    IsTemporary = findData.IsTemporary,
                    CreationTime = findData.CreationTime,
                    LastModifiedTime = findData.LastWriteTime,
                    LastAccessTime = findData.LastAccessTime,
                    DriveLetter = '?',
                };
                entries.Add(entry);
                onEntryIndexed?.Invoke(entry);
            } while (Win32Find.FindNextFileW(handle, out findData));
        }
    }
}
