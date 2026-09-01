namespace ProjectIndexer.Core.FileSystem;

public class FileSystemFactory
{
    public static IFileSystemProvider CreateProvider(char driveLetter)
    {
        driveLetter = char.ToUpperInvariant(driveLetter);

        var driveInfo = GetDriveInfo(driveLetter);
        if (driveInfo == null)
            throw new InvalidOperationException($"Drive {driveLetter}: not found");

        if (!driveInfo.IsReady)
            throw new InvalidOperationException($"Drive {driveLetter}: is not ready");

        string format = driveInfo.DriveFormat;

        if (format.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            return new MftIndexer(driveLetter);

        if (format.Equals("FAT32", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("FAT", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("exFAT", StringComparison.OrdinalIgnoreCase))
            return new FatProvider(driveLetter);

        throw new NotSupportedException(
            $"File system '{format}' on drive {driveLetter}: is not supported");
    }

    public static IFileSystemProvider CreateProviderForUnc(string uncPath)
    {
        if (string.IsNullOrWhiteSpace(uncPath) || !uncPath.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("UNC path required", nameof(uncPath));

        return new SmbProvider(uncPath);
    }

    public static IReadOnlyList<char> GetIndexableDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Where(d => d.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ||
                        d.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) ||
                        d.DriveFormat.Equals("FAT", StringComparison.OrdinalIgnoreCase) ||
                        d.DriveFormat.Equals("exFAT", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Name[0])
            .ToList();
    }

    public static IReadOnlyList<(char DriveLetter, string FileSystem, bool IsReady, long TotalSize, long FreeSpace)> GetAllDrives()
    {
        var drives = new List<(char, string, bool, long, long)>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                char letter = d.Name[0];
                string fs = d.IsReady ? d.DriveFormat : "Unknown";
                bool ready = d.IsReady;
                long total = d.IsReady ? d.TotalSize : 0;
                long free = d.IsReady ? d.AvailableFreeSpace : 0;
                drives.Add((letter, fs, ready, total, free));
            }
            catch
            {
                // Skip inaccessible drives
            }
        }
        return drives;
    }

    public static FileSystemType DetectFileSystemType(char driveLetter)
    {
        var info = GetDriveInfo(driveLetter);
        if (info == null || !info.IsReady) return FileSystemType.Unknown;

        return info.DriveFormat.ToUpperInvariant() switch
        {
            "NTFS" => FileSystemType.Ntfs,
            "FAT32" => FileSystemType.Fat32,
            "FAT" => FileSystemType.Fat32,
            "EXFAT" => FileSystemType.ExFat,
            _ => FileSystemType.Unknown,
        };
    }

    private static DriveInfo? GetDriveInfo(char driveLetter)
    {
        try
        {
            var di = new DriveInfo(driveLetter.ToString());
            return di;
        }
        catch
        {
            return null;
        }
    }
}
