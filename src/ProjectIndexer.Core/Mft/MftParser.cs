using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;

namespace ProjectIndexer.Core.Mft;

internal sealed class MftParser : IDisposable
{
    private const uint FileSignature = 0x454C4946;
    private const uint EndMarker = 0xFFFFFFFF;
    private const int ChunkSize = 64 * 1024 * 1024;
    private const int ProgressInterval = 100_000;

    private readonly char _driveLetter;
    private SafeFileHandle? _driveHandle;
    private BootSector? _bootSector;
    private bool _disposed;
    private readonly int _parallelism;

    public char DriveLetter => _driveLetter;
    internal Action<FileEntry>? EntryParsed { get; set; }

    public MftParser(char driveLetter) : this(driveLetter, Environment.ProcessorCount) { }

    public MftParser(char driveLetter, int parallelism)
    {
        _driveLetter = char.ToUpperInvariant(driveLetter);
        _parallelism = Math.Clamp(parallelism, 1, Environment.ProcessorCount);
    }

    public IEnumerable<FileEntry> ParseAll(IProgress<IndexProgress>? progress = null)
    {
        ThrowIfDisposed();

        var progressInfo = new IndexProgress
        {
            DriveLetter = _driveLetter.ToString(),
            Stage = IndexStage.ReadingBootSector
        };

        try
        {
            OpenDrive();

            progressInfo.Stage = IndexStage.ReadingBootSector;
            progress?.Report(progressInfo);

            byte[] bootSectorBuffer = ArrayPool<byte>.Shared.Rent(NtfsConstants.BootSectorSize);
            try
            {
                ReadExact(0, bootSectorBuffer, NtfsConstants.BootSectorSize);
                _bootSector = BootSector.Parse(bootSectorBuffer.AsSpan(0, NtfsConstants.BootSectorSize).ToArray());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bootSectorBuffer);
            }

            progressInfo.Stage = IndexStage.ReadingMft;
            progress?.Report(progressInfo);

            var (record0Data, mftRuns) = ReadMftRecord0WithRuns();

            try
            {
                int recordSize = _bootSector.MftRecordSize;
                int bytesPerCluster = _bootSector.BytesPerCluster;

                long totalMftBytes = 0;
                foreach (var run in mftRuns)
                {
                    if (run.Lcn > 0)
                    {
                        totalMftBytes += checked(run.ClusterCount * bytesPerCluster);
                    }
                }
                totalMftBytes = Math.Max(totalMftBytes, record0Data.Length);

                long totalRecords = totalMftBytes / recordSize;
                progressInfo.TotalRecords = totalRecords;
                progressInfo.Stage = IndexStage.ParsingRecords;
                progress?.Report(progressInfo);

                int estimatedEntries = EstimateEntryCapacity(totalRecords);
                var entries = new List<FileEntry>(estimatedEntries);

                long globalRecordIndex = 0;
                int fileCount = 0;
                int dirCount = 0;

                foreach (var (lcn, clusterCount) in mftRuns)
                {
                    long recordsInRun = checked(clusterCount * (long)bytesPerCluster / recordSize);

                    if (lcn == 0)
                    {
                        globalRecordIndex += recordsInRun;
                        continue;
                    }

                    long runBytes = checked(clusterCount * (long)bytesPerCluster);
                    long runOffset = checked(lcn * (long)bytesPerCluster);
                    long chunkOffset = 0;

                    while (chunkOffset < runBytes)
                    {
                        int readSize = (int)Math.Min(ChunkSize, runBytes - chunkOffset);
                        readSize -= readSize % recordSize;

                        if (readSize <= 0)
                            break;

                        byte[] buffer = ArrayPool<byte>.Shared.Rent(readSize);
                        try
                        {
                            ReadExact(checked(runOffset + chunkOffset), buffer, readSize);

                            int recordCount = readSize / recordSize;
                            long chunkRecordStart = globalRecordIndex;

                            var localResults = new ConcurrentBag<List<FileEntry>>();

                            ParallelOptions options = new() { MaxDegreeOfParallelism = _parallelism };

                            Parallel.For(0, _parallelism, options, workerId =>
                            {
                                int start = recordCount * workerId / _parallelism;
                                int end = recordCount * (workerId + 1) / _parallelism;

                                if (start >= end)
                                    return;

                                var localEntries = new List<FileEntry>(Math.Max(256, (end - start) / 4));

                                for (int r = start; r < end; r++)
                                {
                                    long recordIndex = chunkRecordStart + r;
                                    int offset = r * recordSize;
                                    Span<byte> record = buffer.AsSpan(offset, recordSize);

                                    if (ReadUInt32(record) != FileSignature)
                                        continue;

                                    if (recordIndex > int.MaxValue)
                                        continue;

                                    var header = MftRecordHeader.Parse(record, (int)recordIndex);
                                    MftRecordHeader.ApplyFixups(record, header.FixupOffset, header.FixupCount,
                                        bytesPerCluster >= _bootSector.BytesPerSector ? _bootSector.BytesPerSector : _bootSector.BytesPerSector);

                                    if (!header.IsInUse || header.HasBaseRecord || header.AttributeOffset >= record.Length)
                                        continue;

                                    // Parse attributes - FIX: Get data size too
                                    ParseRecordAttributes(record[header.AttributeOffset..], out var fileNameAttr, out var siAttr, out long dataSize);

                                    if (!fileNameAttr.HasValue)
                                        continue;

                                    var entry = CreateFileEntry((int)recordIndex, header, fileNameAttr.Value, siAttr, dataSize);
                                    localEntries.Add(entry);
                                }

                                if (localEntries.Count > 0)
                                    localResults.Add(localEntries);
                            });

                            // Merge thread-local lists
                            foreach (var local in localResults)
                            {
                                entries.AddRange(local);
                                for (int i = 0; i < local.Count; i++)
                                {
                                    if (local[i].IsDirectory)
                                        dirCount++;
                                    else
                                        fileCount++;
                                }
                            }

                            globalRecordIndex += recordCount;
                            progressInfo.ParsedRecords = globalRecordIndex;
                            progressInfo.FilesFound = fileCount;
                            progressInfo.DirectoriesFound = dirCount;

                            if (globalRecordIndex % ProgressInterval < recordCount)
                                progress?.Report(progressInfo);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }

                        chunkOffset += readSize;
                    }
                }

                progressInfo.Stage = IndexStage.ReconstructingPaths;
                progress?.Report(progressInfo);

                ReconstructPaths(entries, progressInfo, progress);

                progressInfo.Stage = IndexStage.Completed;
                progressInfo.TotalRecords = totalRecords;
                progressInfo.ParsedRecords = totalRecords;
                progressInfo.FilesFound = fileCount;
                progressInfo.DirectoriesFound = dirCount;
                progress?.Report(progressInfo);

                return entries;
            }
            finally
            {
                // Nothing currently owns record0Data after ParseDataRunsForMft.
            }
        }
        catch (Exception ex)
        {
            progressInfo.Stage = IndexStage.Failed;
            progress?.Report(progressInfo);
            throw new InvalidOperationException($"Failed to parse MFT on drive {_driveLetter}: " + ex.Message, ex);
        }
    }

    // ================================================================
    // PATH RECONSTRUCTION - ORIGINAL (UNCHANGED)
    // ================================================================
    // ================================================================
    // PATH RECONSTRUCTION - FIXED VERSION
    // ================================================================

    // ================================================================
    // PATH RECONSTRUCTION - OPTIMIZED (FAST VERSION)
    // ================================================================

    private void ReconstructPaths(List<FileEntry> entries, IndexProgress progressInfo, IProgress<IndexProgress>? progress)
    {
        int count = entries.Count;
        if (count == 0) return;

        // Use dictionary for fast lookup
        var frnToIndex = new Dictionary<ulong, int>(count);
        var fullPaths = new string[count];
        string root = $"{_driveLetter}:\\";

        // First pass: build FRN to index mapping
        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            frnToIndex[entry.Frn] = i;
            entry.DriveLetter = _driveLetter;

            // Set root path
            if (entry.Frn == NtfsConstants.RootDirectoryFrn)
            {
                fullPaths[i] = root;
            }
        }

        // Second pass: resolve paths iteratively
        // We need multiple passes because parent might appear after child
        bool changed;
        int maxPasses = 10; // Prevent infinite loop
        int pass = 0;

        do
        {
            changed = false;
            pass++;

            for (int i = 0; i < count; i++)
            {
                // Skip if already has path or is root
                if (fullPaths[i] != null)
                    continue;

                var entry = entries[i];

                // Try to find parent
                if (frnToIndex.TryGetValue(entry.ParentFrn, out int parentIndex))
                {
                    string? parentPath = fullPaths[parentIndex];
                    if (parentPath != null)
                    {
                        // Build path
                        string path = parentPath + entry.Name;
                        if (entry.IsDirectory)
                            path += "\\";

                        fullPaths[i] = path;
                        changed = true;
                    }
                }
            }

            // Report progress occasionally
            if (pass % 2 == 0)
            {
                int resolved = 0;
                for (int i = 0; i < count; i++)
                {
                    if (fullPaths[i] != null)
                        resolved++;
                }

                progressInfo.TotalRecords = count;
                progressInfo.ParsedRecords = resolved;
                progress?.Report(progressInfo);
            }

        } while (changed && pass < maxPasses);

        // Final pass: set unknown paths for remaining entries
        string unknown = $"{_driveLetter}:\\$Unknown\\";
        for (int i = 0; i < count; i++)
        {
            if (fullPaths[i] == null)
            {
                var entry = entries[i];
                fullPaths[i] = entry.IsDirectory
                    ? unknown + entry.Name + "\\"
                    : unknown + entry.Name;
            }
            entries[i].FullPath = fullPaths[i];
        }

        // Callback
        var callback = EntryParsed;
        if (callback != null)
        {
            for (int i = 0; i < count; i++)
                callback(entries[i]);
        }

        progressInfo.TotalRecords = count;
        progressInfo.ParsedRecords = count;
        progress?.Report(progressInfo);
    }
    private void ReconstructPathsold(List<FileEntry> entries, IndexProgress progressInfo, IProgress<IndexProgress>? progress)
    {
        int count = entries.Count;
        if (count == 0) return;

        var frnToIndex = new Dictionary<ulong, int>(count, EqualityComparer<ulong>.Default);
        var parentFrns = new ulong[count];
        var firstChild = new int[count];
        var nextSibling = new int[count];
        var fullPaths = new string?[count];

        Array.Fill(firstChild, -1);
        Array.Fill(nextSibling, -1);

        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            frnToIndex[entry.Frn] = i;
            parentFrns[i] = entry.ParentFrn;
            entry.DriveLetter = _driveLetter;
        }

        for (int i = 0; i < count; i++)
        {
            ulong parentFrn = parentFrns[i];
            if (frnToIndex.TryGetValue(parentFrn, out int parentIndex))
            {
                nextSibling[i] = firstChild[parentIndex];
                firstChild[parentIndex] = i;
            }
        }

        string root = $"{_driveLetter}:\\";

        if (frnToIndex.TryGetValue(NtfsConstants.RootDirectoryFrn, out int rootIndex))
        {
            fullPaths[rootIndex] = root;
        }

        var stack = new Stack<int>(Math.Min(count, 8192));

        if (rootIndex >= 0 && rootIndex < count)
        {
            PushChildren(rootIndex, stack, firstChild);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                if (parentFrns[i] == 0)
                    stack.Push(i);
            }
        }

        int processed = 0;

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            string? parentPath = fullPaths[index];

            if (parentPath == null)
                continue;

            int child = firstChild[index];
            while (child >= 0)
            {
                var entry = entries[child];
                string path = parentPath + entry.Name;

                if (entry.IsDirectory)
                    path += "\\";

                fullPaths[child] = path;
                stack.Push(child);
                child = nextSibling[child];
            }

            processed++;

            if (processed % ProgressInterval == 0)
            {
                progressInfo.TotalRecords = count;
                progressInfo.ParsedRecords = processed;
                progressInfo.FilesFound = processed;
                progress?.Report(progressInfo);
            }
        }

        string unknown = $"{_driveLetter}:\\$Unknown\\";

        for (int i = 0; i < count; i++)
        {
            string path = fullPaths[i] ?? BuildUnknownPath(unknown, entries[i]);
            entries[i].FullPath = path;
        }

        var callback = EntryParsed;
        if (callback != null)
        {
            for (int i = 0; i < count; i++)
                callback(entries[i]);
        }

        progressInfo.TotalRecords = count;
        progressInfo.ParsedRecords = count;
        progressInfo.FilesFound = count;
        progress?.Report(progressInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushChildren(int parent, Stack<int> stack, int[] firstChild)
    {
        int child = firstChild[parent];
        while (child >= 0)
        {
            stack.Push(child);
            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildUnknownPath(string unknownDirectory, FileEntry entry)
    {
        return entry.IsDirectory ? unknownDirectory + entry.Name + "\\" : unknownDirectory + entry.Name;
    }

    // ================================================================
    // DRIVE
    // ================================================================

    private void OpenDrive()
    {
        string drivePath = $@"\\.\{_driveLetter}:";
        _driveHandle = Win32Native.CreateFile(
            drivePath,
            Win32Native.GENERIC_READ,
            Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Win32Native.OPEN_EXISTING,
            0x08000000,
            IntPtr.Zero);

        if (_driveHandle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            string message = error switch
            {
                5 => $"Access denied. Administrator privileges required to read drive {_driveLetter}.",
                2 => $"Drive {_driveLetter} not found.",
                _ => $"Failed to open drive {_driveLetter}. Error code: {error}"
            };
            throw new UnauthorizedAccessException(message);
        }
    }

    private void ReadExact(long offset, byte[] buffer, int size)
    {
        if (_driveHandle == null || _driveHandle.IsInvalid)
            throw new InvalidOperationException("Drive handle is not open");

        if (size <= 0)
            return;

        long alignedOffset = offset & ~511L;
        int padding = checked((int)(offset - alignedOffset));
        int readSize = checked((size + padding + 511) & ~511);

        if (readSize > buffer.Length)
            throw new ArgumentException("Buffer is smaller than requested read.");

        if (!Win32Native.SetFilePointerEx(_driveHandle, alignedOffset, out _, Win32Native.FILE_BEGIN))
            throw new IOException($"Failed to seek to offset {offset}.");

        if (!Win32Native.ReadFile(_driveHandle, buffer, (uint)readSize, out uint bytesRead, IntPtr.Zero))
        {
            int error = Marshal.GetLastWin32Error();
            throw new IOException($"Failed to read drive at offset {offset}. Win32 error: {error}");
        }

        if (bytesRead < readSize)
            throw new EndOfStreamException($"Incomplete drive read. Expected {readSize}, got {bytesRead}.");

        if (padding != 0)
            Buffer.BlockCopy(buffer, padding, buffer, 0, size);
    }

    // ================================================================
    // MFT RECORD 0
    // ================================================================

    private (byte[] Record0Data, List<(long Lcn, long ClusterCount)> DataRuns) ReadMftRecord0WithRuns()
    {
        if (_bootSector == null)
            throw new InvalidOperationException("Boot sector not parsed.");

        long mftOffset = _bootSector.MftByteOffset;
        int recordSize = _bootSector.MftRecordSize;

        int initialReadSize = Math.Max(recordSize * 2, Math.Min(_bootSector.BytesPerCluster * 32, 4 * 1024 * 1024));
        initialReadSize -= initialReadSize % recordSize;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(initialReadSize);
        try
        {
            ReadExact(mftOffset, buffer, initialReadSize);
            Span<byte> record = buffer.AsSpan(0, recordSize);

            if (ReadUInt32(record) != FileSignature)
                throw new InvalidOperationException("MFT record 0 not found at expected location.");

            var header = MftRecordHeader.Parse(record, 0);
            MftRecordHeader.ApplyFixups(record, header.FixupOffset, header.FixupCount, _bootSector.BytesPerSector);

            var runs = ParseDataRunsForMft(record[header.AttributeOffset..]);
            byte[] record0 = new byte[recordSize];
            record.CopyTo(record0);
            return (record0, runs);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ================================================================
    // DATA RUNS
    // ================================================================

    private static List<(long Lcn, long ClusterCount)> ParseDataRunsForMft(ReadOnlySpan<byte> attributeData)
    {
        int pos = 0;
        long currentLcn = 0;
        var runs = new List<(long, long)>(16);

        while (pos + 8 <= attributeData.Length)
        {
            uint attrType = ReadUInt32(attributeData[pos..]);

            if (attrType == EndMarker)
                break;

            uint attrLength = ReadUInt32(attributeData[(pos + 4)..]);

            if (attrLength < 0x18 || attrLength > attributeData.Length - pos)
                break;

            int attrEnd = pos + (int)attrLength;
            byte nonResident = attributeData[pos + 8];

            if (attrType == (uint)AttributeType.Data && nonResident != 0)
            {
                if (pos + 64 > attrEnd)
                {
                    pos = attrEnd;
                    continue;
                }

                ushort dataRunOffset = ReadUInt16(attributeData[(pos + 0x20)..]);

                if (dataRunOffset == 0 || dataRunOffset >= attrLength)
                {
                    pos = attrEnd;
                    continue;
                }

                int runPos = pos + dataRunOffset;

                while (runPos < attrEnd)
                {
                    byte header = attributeData[runPos++];

                    if (header == 0)
                        break;

                    int lengthBytes = header & 0x0F;
                    int offsetBytes = header >> 4;

                    if (lengthBytes == 0 || runPos + lengthBytes > attrEnd)
                        break;

                    long clusterCount = ReadVariableLengthUInt(attributeData, ref runPos, lengthBytes);
                    long lcn;

                    if (offsetBytes == 0)
                    {
                        lcn = 0;
                    }
                    else
                    {
                        if (runPos + offsetBytes > attrEnd)
                            break;

                        long delta = ReadVariableLengthSigned(attributeData, ref runPos, offsetBytes);
                        currentLcn += delta;
                        lcn = currentLcn;
                    }

                    if (clusterCount > 0)
                        runs.Add((lcn, clusterCount));
                }

                if (runs.Count > 0)
                    return runs;
            }

            pos = attrEnd;
        }

        throw new InvalidOperationException("Could not find $DATA attribute in MFT record 0.");
    }

    // ================================================================
    // ATTRIBUTE PARSER - FIXED VERSION
    // ================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseRecordAttributes(
        ReadOnlySpan<byte> attrStart,
        out (ulong ParentFrn, string Name, FileNameNamespace Namespace, long AllocatedSize, long ActualSize)? fileNameAttr,
        out (DateTime Created, DateTime Modified, DateTime MftModified, DateTime Accessed, uint FileAttributes)? siAttr,
        out long dataSize)
    {
        fileNameAttr = null;
        siAttr = null;
        dataSize = 0;

        int pos = 0;

        while (pos + 8 <= attrStart.Length)
        {
            uint attrType = ReadUInt32(attrStart[pos..]);

            if (attrType == EndMarker)
                break;

            uint attrLength = ReadUInt32(attrStart[(pos + 4)..]);

            if (attrLength < 0x18 || attrLength > attrStart.Length - pos)
                break;

            int attrEnd = pos + (int)attrLength;

            if (pos + 9 > attrEnd)
                break;

            byte nonResident = attrStart[pos + 8];

            if (attrType == (uint)AttributeType.Data)
            {
                if (nonResident == 0)
                {
                    // Resident data
                    if (pos + 0x18 > attrEnd) { pos = attrEnd; continue; }
                    uint dataLength = ReadUInt32(attrStart[(pos + 0x10)..]);
                    dataSize = dataLength;
                }
                else
                {
                    // Non-resident data
                    if (pos + 0x30 > attrEnd) { pos = attrEnd; continue; }
                    long realSize = ReadInt64(attrStart[(pos + 0x30)..]);
                    dataSize = realSize > 0 ? realSize : 0;
                }
            }

            if (attrType == (uint)AttributeType.FileName && nonResident == 0)
            {
                if (pos + 0x16 > attrEnd) { pos = attrEnd; continue; }

                ushort contentOffset = ReadUInt16(attrStart[(pos + 0x14)..]);
                int dataOffset = pos + contentOffset;

                if (dataOffset < pos || dataOffset + 0x42 > attrEnd)
                {
                    pos = attrEnd;
                    continue;
                }

                ulong parentFrn = ReadUInt64(attrStart[dataOffset..]) & 0x0000FFFFFFFFFFFFUL;
                long allocatedSize = ReadInt64(attrStart[(dataOffset + 0x28)..]);
                long actualSize = ReadInt64(attrStart[(dataOffset + 0x30)..]);

                byte nameLength = attrStart[dataOffset + 0x40];
                byte nameNamespace = attrStart[dataOffset + 0x41];

                int nameBytes = nameLength * 2;
                int nameOffset = dataOffset + 0x42;

                if (nameLength > 0 && nameOffset + nameBytes <= attrEnd)
                {
                    string name = Encoding.Unicode.GetString(attrStart.Slice(nameOffset, nameBytes));

                    // Remove null characters that can cause issues
                    if (name.IndexOf('\0') >= 0)
                        name = name.Replace("\0", string.Empty);

                    fileNameAttr = (parentFrn, name, (FileNameNamespace)nameNamespace, allocatedSize, actualSize);
                }
            }

            if (attrType == (uint)AttributeType.StandardInformation && nonResident == 0)
            {
                if (pos + 0x16 > attrEnd) { pos = attrEnd; continue; }

                ushort contentOffset = ReadUInt16(attrStart[(pos + 0x14)..]);
                int dataOffset = pos + contentOffset;

                if (dataOffset < pos || dataOffset + 0x24 > attrEnd)
                {
                    pos = attrEnd;
                    continue;
                }

                long created = ReadInt64(attrStart[dataOffset..]);
                long modified = ReadInt64(attrStart[(dataOffset + 8)..]);
                long mftModified = ReadInt64(attrStart[(dataOffset + 16)..]);
                long accessed = ReadInt64(attrStart[(dataOffset + 24)..]);
                uint fileAttributes = ReadUInt32(attrStart[(dataOffset + 32)..]);

                siAttr = (
                    DateTime.FromFileTimeUtc(created),
                    DateTime.FromFileTimeUtc(modified),
                    DateTime.FromFileTimeUtc(mftModified),
                    DateTime.FromFileTimeUtc(accessed),
                    fileAttributes
                );
            }

            pos = attrEnd;
        }
    }

    // ================================================================
    // FILE ENTRY - FIXED VERSION
    // ================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FileEntry CreateFileEntry(
        int frn,
        MftRecordHeader header,
        (ulong ParentFrn, string Name, FileNameNamespace Namespace, long AllocatedSize, long ActualSize) fileNameAttr,
        (DateTime Created, DateTime Modified, DateTime MftModified, DateTime Accessed, uint FileAttributes)? siAttr,
        long dataSize)
    {
        var entry = new FileEntry
        {
            Frn = (ulong)(uint)frn,
            Name = fileNameAttr.Name,
            ParentFrn = fileNameAttr.ParentFrn,
            IsDirectory = header.IsDirectory,
            // Use dataSize from $DATA attribute, fallback to ActualSize
            Size = header.IsDirectory ? fileNameAttr.AllocatedSize : Math.Max(dataSize, fileNameAttr.ActualSize),
            AllocatedSize = Math.Max(fileNameAttr.AllocatedSize, 0)
        };

        if (siAttr.HasValue)
        {
            var si = siAttr.Value;
            entry.CreationTime = si.Created;
            entry.LastModifiedTime = si.Modified;
            entry.MftModifiedTime = si.MftModified;
            entry.LastAccessTime = si.Accessed;

            uint fa = si.FileAttributes;
            entry.IsHidden = (fa & 0x2) != 0;
            entry.IsSystem = (fa & 0x4) != 0;
            entry.IsReadOnly = (fa & 0x1) != 0;
            entry.IsArchive = (fa & 0x20) != 0;
            entry.IsTemporary = (fa & 0x100) != 0;
        }

        return entry;
    }

    // ================================================================
    // FAST INTEGER READERS
    // ================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadUInt16(ReadOnlySpan<byte> data)
    {
        return (ushort)(data[0] | (data[1] << 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(ReadOnlySpan<byte> data)
    {
        return (uint)data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16) | ((uint)data[3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64(ReadOnlySpan<byte> data)
    {
        return (ulong)data[0] | ((ulong)data[1] << 8) | ((ulong)data[2] << 16) | ((ulong)data[3] << 24) |
               ((ulong)data[4] << 32) | ((ulong)data[5] << 40) | ((ulong)data[6] << 48) | ((ulong)data[7] << 56);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadInt64(ReadOnlySpan<byte> data)
    {
        return unchecked((long)ReadUInt64(data));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadVariableLengthUInt(ReadOnlySpan<byte> data, ref int pos, int bytes)
    {
        long value = 0;
        for (int i = 0; i < bytes; i++)
        {
            value |= (long)data[pos++] << (i * 8);
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadVariableLengthSigned(ReadOnlySpan<byte> data, ref int pos, int bytes)
    {
        long value = 0;
        for (int i = 0; i < bytes; i++)
        {
            value |= (long)data[pos++] << (i * 8);
        }

        if (bytes > 0)
        {
            int bits = bytes * 8;
            if (bits < 64 && (value & (1L << (bits - 1))) != 0)
            {
                value |= -1L << bits;
            }
        }

        return value;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private static int EstimateEntryCapacity(long totalRecords)
    {
        long estimate = totalRecords / 2;
        estimate = Math.Clamp(estimate, 10_000, 5_000_000);
        return (int)estimate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MftParser));
    }

    // ================================================================
    // DISPOSE
    // ================================================================

    public void Dispose()
    {
        if (_disposed)
            return;

        _driveHandle?.Dispose();
        _driveHandle = null;
        _disposed = true;
    }
}