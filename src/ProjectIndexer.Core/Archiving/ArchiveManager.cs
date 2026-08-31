using Microsoft.Data.Sqlite;
using ProjectIndexer.Core.Indexing;
using ProjectIndexer.Core.Models;
using ProjectIndexer.Core.Searching;

namespace ProjectIndexer.Core.Archiving;

public class ArchiveManager
{
    private readonly string _archiveFolder;
    private readonly SearchEngine _searchEngine;

    public string ArchiveFolder => _archiveFolder;

    public ArchiveManager(string? archiveFolder = null, InMemoryIndex? sharedIndex = null)
    {
        _archiveFolder = archiveFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer", "Archives");

        Directory.CreateDirectory(_archiveFolder);
        _searchEngine = sharedIndex != null ? new SearchEngine(sharedIndex) : null!;
    }

    public string CreateArchive(char driveLetter, IEnumerable<FileEntry> entries, string? volumeSerial = null)
    {
        driveLetter = char.ToUpperInvariant(driveLetter);
        volumeSerial ??= GetVolumeSerial(driveLetter);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string archiveName = $"{driveLetter}_{volumeSerial}_{timestamp}.archive";
        string archivePath = Path.Combine(_archiveFolder, archiveName);

        var entryList = entries.ToList();
        var db = new Archiving.ArchiveDatabase(archivePath);
        db.SaveArchive(entryList, driveLetter);

        return archivePath;
    }

    public List<ArchiveInfo> ListArchives(char? driveLetter = null)
    {
        if (!Directory.Exists(_archiveFolder))
            return [];

        var archives = new List<ArchiveInfo>();
        foreach (string file in Directory.GetFiles(_archiveFolder, "*.archive"))
        {
            try
            {
                var info = new FileInfo(file);
                string name = Path.GetFileNameWithoutExtension(file);
                var parts = name.Split('_');
                if (parts.Length < 3) continue;

                char letter = parts[0].Length > 0 ? char.ToUpperInvariant(parts[0][0]) : '?';

                if (driveLetter.HasValue && letter != driveLetter.Value)
                    continue;

                var db = new Archiving.ArchiveDatabase(file);
                long count = db.GetEntryCount();
                DateTime created = info.CreationTimeUtc;

                archives.Add(new ArchiveInfo
                {
                    FilePath = file,
                    DriveLetter = letter,
                    CreatedAt = created,
                    EntryCount = count,
                    FileSize = info.Length,
                    VolumeSerial = parts.Length > 1 ? parts[1] : "",
                    DriveLabel = GetDriveLabel(letter),
                });
            }
            catch
            {
            }
        }

        return archives.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public List<FileEntry> LoadArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var db = new Archiving.ArchiveDatabase(archivePath);
        return db.LoadAll();
    }

    public List<FileEntry> SearchArchive(string archivePath, string query)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var db = new Archiving.ArchiveDatabase(archivePath);
        var entries = db.LoadAll();

        var index = new InMemoryIndex();
        index.AddRange(entries);

        var engine = new SearchEngine(index);
        return engine.Execute(query);
    }

    public List<(FileEntry Entry, string ArchiveName, ArchiveInfo Info)> SearchAllArchives(string query)
    {
        var results = new List<(FileEntry, string, ArchiveInfo)>();
        var archives = ListArchives();

        foreach (var archive in archives)
        {
            try
            {
                var matches = SearchArchive(archive.FilePath, query);
                foreach (var entry in matches)
                    results.Add((entry, Path.GetFileNameWithoutExtension(archive.FilePath), archive));
            }
            catch
            {
            }
        }

        return results;
    }

    public void DeleteArchive(string archivePath)
    {
        if (File.Exists(archivePath))
            File.Delete(archivePath);
    }

    public void MergeArchives(char driveLetter, int keepCount = 5)
    {
        var archives = ListArchives(driveLetter);
        if (archives.Count <= keepCount) return;

        foreach (var archive in archives.Skip(keepCount))
            DeleteArchive(archive.FilePath);
    }

    public long GetTotalArchiveSize()
    {
        if (!Directory.Exists(_archiveFolder)) return 0;

        return Directory.GetFiles(_archiveFolder, "*.archive")
            .Sum(f => new FileInfo(f).Length);
    }

    public int GetArchiveCount()
    {
        if (!Directory.Exists(_archiveFolder)) return 0;

        return Directory.GetFiles(_archiveFolder, "*.archive").Length;
    }

    public static string GetVolumeSerial(char driveLetter)
    {
        try
        {
            var di = new System.IO.DriveInfo(driveLetter.ToString());
            if (di.IsReady)
            {
                var volumeInfo = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.Name[0] == driveLetter && d.IsReady);
                return volumeInfo?.RootDirectory.ToString()?.GetHashCode().ToString("X8") ?? "00000000";
            }
        }
        catch { }

        return "00000000";
    }

    private static string GetDriveLabel(char driveLetter)
    {
        try
        {
            var di = new System.IO.DriveInfo(driveLetter.ToString());
            return di.IsReady ? di.VolumeLabel : "";
        }
        catch
        {
            return "";
        }
    }
}
