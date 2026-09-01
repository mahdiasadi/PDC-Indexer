using ProjectIndexer.Core.Archiving;
using ProjectIndexer.Core.Database;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Indexing;

public static class FastIndexMigrator
{
    public static void MigrateAllDrives(string? databaseFolder = null)
    {
        databaseFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer");

        var db = new IndexDatabase(databaseFolder);
        
        for (char drive = 'A'; drive <= 'Z'; drive++)
        {
            if (!db.HasIndex(drive)) continue;
            
            Console.WriteLine($"Migrating drive {drive}:...");
            MigrateDrive(db, drive, databaseFolder);
        }
    }

    public static void MigrateDrive(IndexDatabase database, char driveLetter, string? fastIndexFolder = null)
    {
        fastIndexFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer", "FastIndex");

        Directory.CreateDirectory(fastIndexFolder);
        string fastIndexPath = Path.Combine(fastIndexFolder, $"{driveLetter}:");

        if (File.Exists(fastIndexPath + ".idx"))
        {
            Console.WriteLine($"Fast index already exists for {driveLetter}:, skipping.");
            return;
        }

        var entries = database.LoadIndex(driveLetter);
        if (entries.Count == 0)
        {
            Console.WriteLine($"No entries to migrate for {driveLetter}:");
            return;
        }

        Console.WriteLine($"Migrating {entries.Count:N0} entries for {driveLetter}:...");

        var fastIndex = new FastIndex(fastIndexPath);
        fastIndex.AddRange(entries);
        fastIndex.Save();

        Console.WriteLine($"Migration complete for {driveLetter}:. Fast index saved to {fastIndexPath}");
    }

    public static void MigrateArchives(string? archiveFolder = null)
    {
        archiveFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer", "Archives");

        if (!Directory.Exists(archiveFolder))
        {
            Console.WriteLine("Archive folder does not exist.");
            return;
        }

        var oldArchives = Directory.GetFiles(archiveFolder, "*.archive");
        Console.WriteLine($"Found {oldArchives.Length} old archives to migrate.");

        foreach (string oldArchive in oldArchives)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(oldArchive);
                string newBasePath = Path.Combine(archiveFolder, baseName);

                if (File.Exists(newBasePath + ".idx"))
                {
                    Console.WriteLine($"Fast archive index already exists for {baseName}, skipping.");
                    continue;
                }

                Console.WriteLine($"Migrating archive {baseName}...");

                var db = new Archiving.ArchiveDatabase(oldArchive);
                var entries = db.LoadAll();

                if (entries.Count == 0)
                {
                    Console.WriteLine($"  No entries in archive, skipping.");
                    continue;
                }

                var fastIndex = new FastArchiveIndex(newBasePath);
                char driveLetter = entries.FirstOrDefault()?.DriveLetter ?? '?';
                fastIndex.SaveArchive(entries, driveLetter);

                Console.WriteLine($"  Migrated {entries.Count:N0} entries to fast archive index.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error migrating {oldArchive}: {ex.Message}");
            }
        }
    }
}