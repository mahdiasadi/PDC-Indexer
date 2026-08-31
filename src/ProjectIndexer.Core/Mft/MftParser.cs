using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Native;

namespace ProjectIndexer.Core.Mft;

internal class MftParser : IDisposable
{
    private readonly char _driveLetter;
    private SafeFileHandle? _driveHandle;
    private BootSector? _bootSector;
    private bool _disposed;
    private byte[]? _readBuffer;
    private byte[]? _resultBuffer;

    public char DriveLetter => _driveLetter;
    internal Action<FileEntry>? EntryParsed { get; set; }

    public MftParser(char driveLetter)
    {
        _driveLetter = char.ToUpperInvariant(driveLetter);
    }

    public IEnumerable<FileEntry> ParseAll(IProgress<IndexProgress>? progress = null)
    {
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

            var bootSectorData = ReadBytes(0, NtfsConstants.BootSectorSize);
            _bootSector = BootSector.Parse(bootSectorData);

            progressInfo.Stage = IndexStage.ReadingMft;
            progress?.Report(progressInfo);

            var (mftRecord0, mftDataRuns) = ReadMftRecord0WithRuns();

            long totalMftSize = 0;
            foreach (var (lcn, count) in mftDataRuns)
                if (lcn != 0) totalMftSize += count * _bootSector.BytesPerCluster;

            totalMftSize = Math.Max(totalMftSize, (long)mftRecord0.Length);
            int mftRecordSize = _bootSector.MftRecordSize;
            long totalRecords = totalMftSize / mftRecordSize;
            progressInfo.TotalRecords = totalRecords;
            progressInfo.Stage = IndexStage.ParsingRecords;
            progress?.Report(progressInfo);

            int estimatedEntries = (int)(totalMftSize / (mftRecordSize * 2));
            var entries = new List<FileEntry>(Math.Max(estimatedEntries, 10000));
            long globalRecordIndex = 0;
            int fileCount = 0, dirCount = 0;

            foreach (var (lcn, clusterCount) in mftDataRuns)
            {
                if (lcn == 0)
                {
                    globalRecordIndex += clusterCount * _bootSector.BytesPerCluster / mftRecordSize;
                    continue;
                }

                long volumeOffset = lcn * _bootSector.BytesPerCluster;
                long clusterBytes = clusterCount * _bootSector.BytesPerCluster;
                int chunkSize = (int)Math.Min(clusterBytes, 64 * 1024 * 1024);
                long chunkOffset = 0;

                while (chunkOffset < clusterBytes)
                {
                    int readSize = (int)Math.Min(chunkSize, clusterBytes - chunkOffset);
                    byte[] chunkData = ReadBytes(volumeOffset + chunkOffset, readSize);
                    int recordsInChunk = readSize / mftRecordSize;

                    for (int r = 0; r < recordsInChunk; r++)
                    {
                        int offset = r * mftRecordSize;
                        var recordSpan = new Span<byte>(chunkData, offset, mftRecordSize);
                        uint sig = BitConverter.ToUInt32(recordSpan);
                        if (sig != 0x454C4946) { globalRecordIndex++; continue; }

                        var header = MftRecordHeader.Parse(recordSpan, (int)globalRecordIndex);
                        MftRecordHeader.ApplyFixups(recordSpan, header.FixupOffset, header.FixupCount, _bootSector.BytesPerSector);

                        if (!header.IsInUse || header.HasBaseRecord)
                        {
                            progressInfo.ParsedRecords = ++globalRecordIndex;
                            if (globalRecordIndex % 5000 == 0) progress?.Report(progressInfo);
                            continue;
                        }

                        int idx = (int)globalRecordIndex;
                        globalRecordIndex++;

                        ParseRecordAttributes(recordSpan[header.AttributeOffset..], header, out var fileNameAttr, out var siAttr);

                        if (fileNameAttr != null)
                        {
                            var entry = CreateFileEntry(idx, header, fileNameAttr.Value, siAttr);
                            entries.Add(entry);
                            if (entry.IsDirectory) dirCount++; else fileCount++;
                        }

                        progressInfo.ParsedRecords = idx + 1;
                        progressInfo.FilesFound = fileCount;
                        progressInfo.DirectoriesFound = dirCount;
                        if (idx % 5000 == 0) progress?.Report(progressInfo);
                    }

                    chunkOffset += readSize;
                }
            }

            progressInfo.Stage = IndexStage.ReconstructingPaths;
            progress?.Report(progressInfo);

            var childMap = new Dictionary<ulong, List<FileEntry>>(entries.Count);
            foreach (var entry in entries)
            {
                entry.DriveLetter = _driveLetter;
                if (!childMap.TryGetValue(entry.ParentFrn, out var list))
                    childMap[entry.ParentFrn] = list = [];
                list.Add(entry);
            }

            var rootDir = $"{_driveLetter}:\\";
            var bfsQueue = new Queue<(FileEntry Entry, string ParentPath)>();
            int bfsCount = 0;

            if (childMap.TryGetValue(NtfsConstants.RootDirectoryFrn, out var rootChildren))
            {
                foreach (var child in rootChildren)
                    bfsQueue.Enqueue((child, rootDir));
            }

            while (bfsQueue.Count > 0)
            {
                var (entry, parentPath) = bfsQueue.Dequeue();
                entry.FullPath = string.Concat(parentPath, entry.Name);
                if (entry.IsDirectory)
                    entry.FullPath = string.Concat(entry.FullPath, "\\");
                EntryParsed?.Invoke(entry);
                bfsCount++;

                if (entry.IsDirectory && childMap.TryGetValue(entry.Frn, out var children))
                {
                    string childBase = entry.FullPath;
                    foreach (var child in children)
                        bfsQueue.Enqueue((child, childBase));
                }

                if (bfsCount % 10000 == 0)
                {
                    progressInfo.FilesFound = bfsCount;
                    progress?.Report(progressInfo);
                }
            }

            // Fallback: assign paths to any entries not reached by BFS (orphaned entries)
            string unknownDir = $"{_driveLetter}:\\$Unknown\\";
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FullPath))
                {
                    entry.FullPath = string.Concat(unknownDir, entry.Name);
                    if (entry.IsDirectory)
                        entry.FullPath = string.Concat(entry.FullPath, "\\");
                    EntryParsed?.Invoke(entry);
                }
            }

            progressInfo.Stage = IndexStage.Completed;
            progress?.Report(progressInfo);

            return entries;
        }
        catch (Exception ex)
        {
            progressInfo.Stage = IndexStage.Failed;
            progress?.Report(progressInfo);
            throw new InvalidOperationException($"Failed to parse MFT on drive {_driveLetter}: {ex.Message}", ex);
        }
    }

    private void OpenDrive()
    {
        string drivePath = $@"\\.\{_driveLetter}:";
        _driveHandle = Win32Native.CreateFile(
            drivePath,
            Win32Native.GENERIC_READ,
            Win32Native.FILE_SHARE_READ | Win32Native.FILE_SHARE_WRITE,
            IntPtr.Zero,
            Win32Native.OPEN_EXISTING,
            Win32Native.FILE_FLAG_RANDOM_ACCESS,
            IntPtr.Zero);

        if (_driveHandle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            string message = error switch
            {
                5 => $"Access denied. Administrator privileges required to read drive {_driveLetter}.",
                2 => $"Drive {_driveLetter} not found.",
                _ => $"Failed to open drive {_driveLetter}. Error code: {error}",
            };
            throw new UnauthorizedAccessException(message);
        }
    }

    private byte[] ReadBytes(long offset, int size)
    {
        if (_driveHandle == null || _driveHandle.IsInvalid)
            throw new InvalidOperationException("Drive handle is not open");

        long alignedOffset = offset & ~(0x1FF);
        int padding = (int)(offset - alignedOffset);
        int readSize = (size + padding + 0x1FF) & ~0x1FF;

        if (_readBuffer == null || _readBuffer.Length < readSize)
            _readBuffer = new byte[readSize];
        if (_resultBuffer == null || _resultBuffer.Length < size)
            _resultBuffer = new byte[size];

        if (!Win32Native.SetFilePointerEx(_driveHandle, alignedOffset, out _, Win32Native.FILE_BEGIN))
            throw new InvalidOperationException("Failed to set file pointer");

        if (!Win32Native.ReadFile(_driveHandle, _readBuffer, (uint)readSize, out uint bytesRead, IntPtr.Zero))
            throw new InvalidOperationException($"Failed to read from drive at offset {offset}");

        Buffer.BlockCopy(_readBuffer, padding, _resultBuffer, 0, size);
        return _resultBuffer;
    }
    private (byte[] Record0Data, IEnumerable<(long Lcn, long ClusterCount)> DataRuns) ReadMftRecord0WithRuns()
    {
        if (_bootSector == null)
            throw new InvalidOperationException("Boot sector not parsed");

        long mftOffset = _bootSector.MftByteOffset;
        int mftRecordSize = _bootSector.MftRecordSize;

        int initialReadSize = Math.Max(mftRecordSize * 2, Math.Min(_bootSector.BytesPerCluster * 32, 16 * 1024 * 1024));

        var firstChunk = ReadBytes(mftOffset, initialReadSize);

        var record0Span = new Span<byte>(firstChunk, 0, mftRecordSize);

        uint sig = BitConverter.ToUInt32(record0Span);
        if (sig != 0x454C4946)
            throw new InvalidOperationException("MFT record 0 not found at expected location");

        var header0 = MftRecordHeader.Parse(record0Span, 0);
        MftRecordHeader.ApplyFixups(record0Span, header0.FixupOffset, header0.FixupCount, _bootSector.BytesPerSector);

        var dataRuns = ParseDataRunsForMft(record0Span[header0.AttributeOffset..]);
        var record0Data = firstChunk[..mftRecordSize];

        return (record0Data, dataRuns);
    }

    private IEnumerable<(long Lcn, long ClusterCount)> ParseDataRunsForMft(ReadOnlySpan<byte> attributeData)
    {
        int pos = 0;
        long currentLcn = 0;

        while (pos + 22 <= attributeData.Length)
        {
            uint attrType = BitConverter.ToUInt32(attributeData[pos..]);
            if (attrType == 0xFFFFFFFF) break;

            uint attrLength = BitConverter.ToUInt32(attributeData[(pos + 4)..]);
            if (attrLength == 0 || pos + attrLength > attributeData.Length) break;

            byte nonResident = attributeData[pos + 8];

            if (attrType == (uint)AttributeType.Data)
            {
                if (nonResident != 0)
                {
                    if (pos + 64 > attributeData.Length)
                    {
                        pos += (int)attrLength;
                        continue;
                    }

                    ushort dataRunOffset = BitConverter.ToUInt16(attributeData[(pos + 0x20)..]);
                    if (dataRunOffset == 0 || dataRunOffset >= attrLength)
                    {
                        pos += (int)attrLength;
                        continue;
                    }

                    int dataRunPos = pos + dataRunOffset;

                    var runs = new List<(long, long)>();
                    while (dataRunPos + 1 <= pos + attrLength)
                    {
                        byte header = attributeData[dataRunPos++];
                        int lengthBytes = header & 0x0F;
                        int offsetBytes = header >> 4;

                        if (lengthBytes == 0) break;

                        if (dataRunPos + lengthBytes > pos + attrLength) break;
                        long clusterCount = ReadVariableLengthInt(attributeData, ref dataRunPos, lengthBytes);

                        if (offsetBytes > 0)
                        {
                            if (dataRunPos + offsetBytes > pos + attrLength) break;
                            long lcnOffset = ReadVariableLengthSignedInt(attributeData, ref dataRunPos, offsetBytes);
                            currentLcn += lcnOffset;
                            runs.Add((currentLcn, clusterCount));
                        }
                        else
                        {
                            runs.Add((0, clusterCount));
                        }
                    }

                    if (runs.Count > 0)
                        return runs;
                }
            }

            pos += (int)attrLength;
        }

        throw new InvalidOperationException(
            "Could not find $DATA attribute in MFT record 0. The NTFS volume may have an unsupported layout.");
    }

    private static long ReadVariableLengthInt(ReadOnlySpan<byte> data, ref int pos, int bytes)
    {
        long result = 0;
        for (int i = 0; i < bytes; i++)
            result |= (long)data[pos++] << (i * 8);
        return result;
    }

    private static long ReadVariableLengthSignedInt(ReadOnlySpan<byte> data, ref int pos, int bytes)
    {
        long result = 0;
        for (int i = 0; i < bytes; i++)
            result |= (long)data[pos++] << (i * 8);

        if (bytes > 0 && (result & (1L << ((bytes * 8) - 1))) != 0)
        {
            for (int i = bytes; i < 8; i++)
                result |= ~0L << (bytes * 8);
        }

        return result;
    }

    private static void ParseRecordAttributes(
        ReadOnlySpan<byte> attrStart,
        MftRecordHeader header,
        out (ulong ParentFrn, string Name, FileNameNamespace Namespace, long AllocatedSize, long ActualSize)? fileNameAttr,
        out (DateTime Created, DateTime Modified, DateTime MftModified, DateTime Accessed, uint FileAttributes)? siAttr)
    {
        fileNameAttr = null;
        siAttr = null;
        int pos = 0;

        while (pos < attrStart.Length)
        {
            uint attrType = BitConverter.ToUInt32(attrStart[pos..]);
            if (attrType == 0xFFFFFFFF) break;

            uint attrLength = BitConverter.ToUInt32(attrStart[(pos + 4)..]);
            if (attrLength == 0 || pos + attrLength > attrStart.Length) break;

            byte nonResident = attrStart[pos + 8];

            if (attrType == (uint)AttributeType.FileName && nonResident == 0)
            {
                ushort contentOffset = BitConverter.ToUInt16(attrStart[(pos + 0x14)..]);
                int dataOffset = pos + contentOffset;

                ulong parentFrn = BitConverter.ToUInt64(attrStart[dataOffset..]) & 0x0000FFFFFFFFFFFF;
                long allocSize = BitConverter.ToInt64(attrStart[(dataOffset + 0x28)..]);
                long actualSize = BitConverter.ToInt64(attrStart[(dataOffset + 0x30)..]);
                byte nameLength = attrStart[dataOffset + 0x40];
                byte nameNamespace = attrStart[dataOffset + 0x41];

                if (nameLength > 0)
                {
                    string name = System.Text.Encoding.Unicode.GetString(
                        attrStart.Slice(dataOffset + 0x42, nameLength * 2));

                    fileNameAttr = (parentFrn, name, (FileNameNamespace)nameNamespace, allocSize, actualSize);
                }
            }
            else if (attrType == (uint)AttributeType.StandardInformation && nonResident == 0)
            {
                ushort contentOffset = BitConverter.ToUInt16(attrStart[(pos + 0x14)..]);
                int dataOffset = pos + contentOffset;

                if (dataOffset + 0x48 <= attrStart.Length)
                {
                    long created = BitConverter.ToInt64(attrStart[dataOffset..]);
                    long modified = BitConverter.ToInt64(attrStart[(dataOffset + 8)..]);
                    long mftModified = BitConverter.ToInt64(attrStart[(dataOffset + 16)..]);
                    long accessed = BitConverter.ToInt64(attrStart[(dataOffset + 24)..]);
                    uint fileAttributes = BitConverter.ToUInt32(attrStart[(dataOffset + 32)..]);

                    siAttr = (
                        DateTime.FromFileTimeUtc(created),
                        DateTime.FromFileTimeUtc(modified),
                        DateTime.FromFileTimeUtc(mftModified),
                        DateTime.FromFileTimeUtc(accessed),
                        fileAttributes);
                }
            }

            pos += (int)attrLength;
        }
    }

    private static FileEntry CreateFileEntry(
        int frn,
        MftRecordHeader header,
        (ulong ParentFrn, string Name, FileNameNamespace Namespace, long AllocatedSize, long ActualSize) fileNameAttr,
        (DateTime Created, DateTime Modified, DateTime MftModified, DateTime Accessed, uint FileAttributes)? siAttr)
    {
        var entry = new FileEntry
        {
            Frn = (ulong)frn,
            Name = fileNameAttr.Name,
            ParentFrn = fileNameAttr.ParentFrn,
            IsDirectory = header.IsDirectory,
            Size = Math.Max(0, fileNameAttr.ActualSize),
            AllocatedSize = Math.Max(0, fileNameAttr.AllocatedSize),
        };

        if (siAttr.HasValue)
        {
            entry.CreationTime = siAttr.Value.Created;
            entry.LastModifiedTime = siAttr.Value.Modified;
            entry.MftModifiedTime = siAttr.Value.MftModified;
            entry.LastAccessTime = siAttr.Value.Accessed;

            uint fa = siAttr.Value.FileAttributes;
            entry.IsHidden = (fa & 0x2) != 0;
            entry.IsSystem = (fa & 0x4) != 0;
            entry.IsReadOnly = (fa & 0x1) != 0;
            entry.IsArchive = (fa & 0x20) != 0;
            entry.IsTemporary = (fa & 0x100) != 0;
        }

        return entry;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _driveHandle?.Dispose();
            _disposed = true;
        }
    }
}
