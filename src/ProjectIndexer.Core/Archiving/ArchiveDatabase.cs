using Microsoft.Data.Sqlite;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Archiving;

internal class ArchiveDatabase
{
    private readonly string _connectionString;

    public ArchiveDatabase(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Pooling=False";
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ArchiveEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Frn INTEGER NOT NULL DEFAULT 0,
                Name TEXT NOT NULL,
                FullPath TEXT NOT NULL,
                ParentFrn INTEGER NOT NULL DEFAULT 0,
                Size INTEGER NOT NULL DEFAULT 0,
                AllocatedSize INTEGER NOT NULL DEFAULT 0,
                CreationTime TEXT,
                LastModifiedTime TEXT,
                LastAccessTime TEXT,
                MftModifiedTime TEXT,
                IsDirectory INTEGER NOT NULL DEFAULT 0,
                IsHidden INTEGER NOT NULL DEFAULT 0,
                IsReadOnly INTEGER NOT NULL DEFAULT 0,
                IsSystem INTEGER NOT NULL DEFAULT 0,
                IsArchive INTEGER NOT NULL DEFAULT 0,
                IsTemporary INTEGER NOT NULL DEFAULT 0,
                DriveLetter TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_archive_name ON ArchiveEntries(Name);
            CREATE INDEX IF NOT EXISTS idx_archive_path ON ArchiveEntries(FullPath);

            CREATE TABLE IF NOT EXISTS ArchiveMeta (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            INSERT OR IGNORE INTO ArchiveMeta (Key, Value)
            VALUES ('CreatedAt', datetime('now'));
            """;
        cmd.ExecuteNonQuery();
    }

    public void SaveArchive(List<FileEntry> entries, char driveLetter)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var transaction = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ArchiveEntries
                (Frn, Name, FullPath, ParentFrn, Size, AllocatedSize,
                 CreationTime, LastModifiedTime, LastAccessTime, MftModifiedTime,
                 IsDirectory, IsHidden, IsReadOnly, IsSystem, IsArchive, IsTemporary,
                 DriveLetter)
            VALUES
                ($frn, $name, $path, $parent, $size, $allocSize,
                 $created, $modified, $accessed, $mftModified,
                 $isDir, $hidden, $readOnly, $system, $archive, $temporary,
                 $drive)
            """;

        foreach (var entry in entries)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$frn", (long)entry.Frn);
            cmd.Parameters.AddWithValue("$name", entry.Name);
            cmd.Parameters.AddWithValue("$path", entry.FullPath);
            cmd.Parameters.AddWithValue("$parent", (long)entry.ParentFrn);
            cmd.Parameters.AddWithValue("$size", entry.Size);
            cmd.Parameters.AddWithValue("$allocSize", entry.AllocatedSize);
            cmd.Parameters.AddWithValue("$created", (object?)entry.CreationTime?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$modified", (object?)entry.LastModifiedTime?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$accessed", (object?)entry.LastAccessTime?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mftModified", (object?)entry.MftModifiedTime?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isDir", entry.IsDirectory ? 1 : 0);
            cmd.Parameters.AddWithValue("$hidden", entry.IsHidden ? 1 : 0);
            cmd.Parameters.AddWithValue("$readOnly", entry.IsReadOnly ? 1 : 0);
            cmd.Parameters.AddWithValue("$system", entry.IsSystem ? 1 : 0);
            cmd.Parameters.AddWithValue("$archive", entry.IsArchive ? 1 : 0);
            cmd.Parameters.AddWithValue("$temporary", entry.IsTemporary ? 1 : 0);
            cmd.Parameters.AddWithValue("$drive", entry.DriveLetter.ToString());
            cmd.ExecuteNonQuery();
        }

        using var metaCmd = conn.CreateCommand();
        metaCmd.CommandText = "INSERT OR REPLACE INTO ArchiveMeta (Key, Value) VALUES ('DriveLetter', $drive)";
        metaCmd.Parameters.AddWithValue("$drive", driveLetter.ToString());
        metaCmd.ExecuteNonQuery();

        metaCmd.CommandText = "INSERT OR REPLACE INTO ArchiveMeta (Key, Value) VALUES ('EntryCount', $count)";
        metaCmd.Parameters.AddWithValue("$count", entries.Count.ToString());
        metaCmd.ExecuteNonQuery();

        transaction.Commit();
    }

    public List<FileEntry> LoadAll()
    {
        var entries = new List<FileEntry>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ArchiveEntries ORDER BY Id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var entry = new FileEntry
            {
                Frn = (ulong)reader.GetInt64(reader.GetOrdinal("Frn")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                FullPath = reader.GetString(reader.GetOrdinal("FullPath")),
                ParentFrn = (ulong)reader.GetInt64(reader.GetOrdinal("ParentFrn")),
                Size = reader.GetInt64(reader.GetOrdinal("Size")),
                AllocatedSize = reader.GetInt64(reader.GetOrdinal("AllocatedSize")),
                IsDirectory = reader.GetInt32(reader.GetOrdinal("IsDirectory")) != 0,
                IsHidden = reader.GetInt32(reader.GetOrdinal("IsHidden")) != 0,
                IsReadOnly = reader.GetInt32(reader.GetOrdinal("IsReadOnly")) != 0,
                IsSystem = reader.GetInt32(reader.GetOrdinal("IsSystem")) != 0,
                IsArchive = reader.GetInt32(reader.GetOrdinal("IsArchive")) != 0,
                IsTemporary = reader.GetInt32(reader.GetOrdinal("IsTemporary")) != 0,
                DriveLetter = reader.GetString(reader.GetOrdinal("DriveLetter"))[0],
            };

            entry.CreationTime = ReadNullableDateTime(reader, "CreationTime");
            entry.LastModifiedTime = ReadNullableDateTime(reader, "LastModifiedTime");
            entry.LastAccessTime = ReadNullableDateTime(reader, "LastAccessTime");
            entry.MftModifiedTime = ReadNullableDateTime(reader, "MftModifiedTime");

            entries.Add(entry);
        }

        return entries;
    }

    public long GetEntryCount()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM ArchiveEntries";
            return (long)cmd.ExecuteScalar()!;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;

        string? value = reader.GetString(ordinal);
        if (string.IsNullOrEmpty(value)) return null;

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;

        return null;
    }
}
