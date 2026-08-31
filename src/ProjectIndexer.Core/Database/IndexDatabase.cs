using Microsoft.Data.Sqlite;
using ProjectIndexer.Core.Models;
using System.IO.Compression;
using System.Text;

namespace ProjectIndexer.Core.Database;

public class IndexDatabase
{
    private readonly string _connectionString;

    public IndexDatabase(string? folderPath = null)
    {
        folderPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectIndexer");

        Directory.CreateDirectory(folderPath);
        string dbPath = Path.Combine(folderPath, "index.db");
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();
    }

    private static byte[] CompressForStorage(string value)
    {
        byte[] raw = Encoding.UTF8.GetBytes(value);
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Fastest))
            ds.Write(raw);
        byte[] comp = ms.ToArray();
        if (comp.Length < raw.Length)
        {
            byte[] result = new byte[comp.Length + 1];
            result[0] = 1;
            Buffer.BlockCopy(comp, 0, result, 1, comp.Length);
            return result;
        }
        byte[] rawResult = new byte[raw.Length + 1];
        rawResult[0] = 0;
        Buffer.BlockCopy(raw, 0, rawResult, 1, raw.Length);
        return rawResult;
    }

    private static string DecompressFromStorage(byte[] data)
    {
        if (data.Length == 0) return "";
        int offset = data[0] == 1 ? 1 : 0;
        if (data[0] == 1)
        {
            using var ms = new MemoryStream(data, 1, data.Length - 1);
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var r = new StreamReader(ds, Encoding.UTF8);
            return r.ReadToEnd();
        }
        return Encoding.UTF8.GetString(data, 1, data.Length - 1);
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS FileEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Frn INTEGER NOT NULL DEFAULT 0,
                Name TEXT NOT NULL,
                FullPath TEXT NOT NULL,
                ParentFrn INTEGER NOT NULL DEFAULT 0,
                Size INTEGER NOT NULL DEFAULT 0,
                AllocatedSize INTEGER NOT NULL DEFAULT 0,
                CreationTime INTEGER,
                LastModifiedTime INTEGER,
                LastAccessTime INTEGER,
                MftModifiedTime INTEGER,
                IsDirectory INTEGER NOT NULL DEFAULT 0,
                IsHidden INTEGER NOT NULL DEFAULT 0,
                IsReadOnly INTEGER NOT NULL DEFAULT 0,
                IsSystem INTEGER NOT NULL DEFAULT 0,
                IsArchive INTEGER NOT NULL DEFAULT 0,
                IsTemporary INTEGER NOT NULL DEFAULT 0,
                DriveLetter INTEGER NOT NULL DEFAULT 0,
                IndexedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_name ON FileEntries(Name);
            CREATE INDEX IF NOT EXISTS idx_fullpath ON FileEntries(FullPath);
            CREATE INDEX IF NOT EXISTS idx_drive ON FileEntries(DriveLetter);
            CREATE INDEX IF NOT EXISTS idx_frn ON FileEntries(Frn);

            CREATE TABLE IF NOT EXISTS IndexMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;

        cmd.ExecuteNonQuery();
    }

    public void AppendBatch(IEnumerable<FileEntry> entries, char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var txn = conn.BeginTransaction();
        AppendBatch(conn, entries, driveLetter);
        txn.Commit();
    }

    public void SaveIndex(IEnumerable<FileEntry> entries, char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var transaction = conn.BeginTransaction();

        using var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM FileEntries WHERE DriveLetter = @drive";
        deleteCmd.Parameters.AddWithValue("@drive", (int)driveLetter);
        deleteCmd.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO FileEntries 
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

        var frnParam = cmd.Parameters.Add("$frn", SqliteType.Integer);
        var nameParam = cmd.Parameters.Add("$name", SqliteType.Text);
        var pathParam = cmd.Parameters.Add("$path", SqliteType.Blob);
        var parentParam = cmd.Parameters.Add("$parent", SqliteType.Integer);
        var sizeParam = cmd.Parameters.Add("$size", SqliteType.Integer);
        var allocParam = cmd.Parameters.Add("$allocSize", SqliteType.Integer);
        var createdParam = cmd.Parameters.Add("$created", SqliteType.Integer);
        var modifiedParam = cmd.Parameters.Add("$modified", SqliteType.Integer);
        var accessedParam = cmd.Parameters.Add("$accessed", SqliteType.Integer);
        var mftParam = cmd.Parameters.Add("$mftModified", SqliteType.Integer);
        var isDirParam = cmd.Parameters.Add("$isDir", SqliteType.Integer);
        var hiddenParam = cmd.Parameters.Add("$hidden", SqliteType.Integer);
        var readOnlyParam = cmd.Parameters.Add("$readOnly", SqliteType.Integer);
        var systemParam = cmd.Parameters.Add("$system", SqliteType.Integer);
        var archiveParam = cmd.Parameters.Add("$archive", SqliteType.Integer);
        var temporaryParam = cmd.Parameters.Add("$temporary", SqliteType.Integer);
        var driveParam = cmd.Parameters.Add("$drive", SqliteType.Integer);

        foreach (var entry in entries)
        {
            frnParam.Value = (long)entry.Frn;
            nameParam.Value = entry.Name;
            pathParam.Value = CompressForStorage(entry.FullPath);
            parentParam.Value = (long)entry.ParentFrn;
            sizeParam.Value = entry.Size;
            allocParam.Value = entry.AllocatedSize;
            createdParam.Value = (object?)entry.CreationTime?.ToFileTimeUtc() ?? DBNull.Value;
            modifiedParam.Value = (object?)entry.LastModifiedTime?.ToFileTimeUtc() ?? DBNull.Value;
            accessedParam.Value = (object?)entry.LastAccessTime?.ToFileTimeUtc() ?? DBNull.Value;
            mftParam.Value = (object?)entry.MftModifiedTime?.ToFileTimeUtc() ?? DBNull.Value;
            isDirParam.Value = entry.IsDirectory ? 1 : 0;
            hiddenParam.Value = entry.IsHidden ? 1 : 0;
            readOnlyParam.Value = entry.IsReadOnly ? 1 : 0;
            systemParam.Value = entry.IsSystem ? 1 : 0;
            archiveParam.Value = entry.IsArchive ? 1 : 0;
            temporaryParam.Value = entry.IsTemporary ? 1 : 0;
            driveParam.Value = (int)entry.DriveLetter;
            cmd.ExecuteNonQuery();
        }

        using var metaCmd = conn.CreateCommand();
        metaCmd.CommandText = """
            INSERT OR REPLACE INTO IndexMetadata (Key, Value)
            VALUES ('LastIndexTime_' || @drive, datetime('now'))
            """;
        metaCmd.Parameters.AddWithValue("@drive", (int)driveLetter);
        metaCmd.ExecuteNonQuery();

        transaction.Commit();
    }

    public List<FileEntry> LoadIndex(char driveLetter)
    {
                var entries = new List<FileEntry>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FileEntries WHERE DriveLetter = @drive ORDER BY Id";
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var entry = new FileEntry
            {
                Frn = (ulong)reader.GetInt64(reader.GetOrdinal("Frn")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                FullPath = ReadFullPath(reader),
                ParentFrn = (ulong)reader.GetInt64(reader.GetOrdinal("ParentFrn")),
                Size = reader.GetInt64(reader.GetOrdinal("Size")),
                AllocatedSize = reader.GetInt64(reader.GetOrdinal("AllocatedSize")),
                IsDirectory = reader.GetInt32(reader.GetOrdinal("IsDirectory")) != 0,
                IsHidden = reader.GetInt32(reader.GetOrdinal("IsHidden")) != 0,
                IsReadOnly = reader.GetInt32(reader.GetOrdinal("IsReadOnly")) != 0,
                IsSystem = reader.GetInt32(reader.GetOrdinal("IsSystem")) != 0,
                IsArchive = reader.GetInt32(reader.GetOrdinal("IsArchive")) != 0,
                IsTemporary = reader.GetInt32(reader.GetOrdinal("IsTemporary")) != 0,
                DriveLetter = ReadDriveLetter(reader),
            };

            entry.CreationTime = ReadNullableDateTime(reader, "CreationTime");
            entry.LastModifiedTime = ReadNullableDateTime(reader, "LastModifiedTime");
            entry.LastAccessTime = ReadNullableDateTime(reader, "LastAccessTime");
            entry.MftModifiedTime = ReadNullableDateTime(reader, "MftModifiedTime");

            entries.Add(entry);
        }

        return entries;
    }

    public bool HasIndex(char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM FileEntries WHERE DriveLetter = @drive";
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);

        return (long)cmd.ExecuteScalar()! > 0;
    }

    public long GetEntryCount(char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM FileEntries WHERE DriveLetter = @drive";
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);

        return (long)cmd.ExecuteScalar()!;
    }

    public DateTime? GetLastIndexTime(char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM IndexMetadata WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", $"LastIndexTime_{driveLetter}");

        var result = cmd.ExecuteScalar();
        if (result is string s && DateTime.TryParse(s, out var dt))
            return dt;

        return null;
    }

    public void SetLastIndexTime(char driveLetter, DateTime time)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO IndexMetadata (Key, Value)
            VALUES (@key, @val)
            """;
        cmd.Parameters.AddWithValue("@key", $"LastIndexTime_{driveLetter}");
        cmd.Parameters.AddWithValue("@val", time.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void SaveJournalState(char driveLetter, long journalId, long nextUsn)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO IndexMetadata (Key, Value)
            VALUES (@keyId, @valId)
            """;
        cmd.Parameters.AddWithValue("@keyId", $"UsnJournalId_{driveLetter}");
        cmd.Parameters.AddWithValue("@valId", journalId.ToString());
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
            INSERT OR REPLACE INTO IndexMetadata (Key, Value)
            VALUES (@keyUsn, @valUsn)
            """;
        cmd.Parameters.AddWithValue("@keyUsn", $"UsnNextUsn_{driveLetter}");
        cmd.Parameters.AddWithValue("@valUsn", nextUsn.ToString());
        cmd.ExecuteNonQuery();
    }

    public bool LoadJournalState(char driveLetter, out long journalId, out long nextUsn)
    {
                journalId = 0;
        nextUsn = 0;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM IndexMetadata WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", $"UsnJournalId_{driveLetter}");

        var result = cmd.ExecuteScalar();
        if (result is string idStr && long.TryParse(idStr, out var jId))
            journalId = jId;
        else
            return false;

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT Value FROM IndexMetadata WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", $"UsnNextUsn_{driveLetter}");

        result = cmd.ExecuteScalar();
        if (result is string usnStr && long.TryParse(usnStr, out var nUsn))
            nextUsn = nUsn;
        else
            return false;

        return true;
    }

    public void InsertOrUpdateEntry(FileEntry entry, char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM FileEntries WHERE Frn = @frn AND DriveLetter = @drive";
        cmd.Parameters.AddWithValue("@frn", (long)entry.Frn);
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);

        bool exists = (long)cmd.ExecuteScalar()! > 0;

        cmd.Parameters.Clear();
        if (exists)
        {
            cmd.CommandText = """
                UPDATE FileEntries SET Name = @name, FullPath = @path, ParentFrn = @parent,
                    Size = @size, IsDirectory = @isDir, IsHidden = @hidden, IsReadOnly = @readOnly,
                    IsSystem = @system, IsArchive = @archive, IsTemporary = @temporary
                WHERE Frn = @frn AND DriveLetter = @drive
                """;
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO FileEntries (Frn, Name, FullPath, ParentFrn, Size, AllocatedSize,
                    CreationTime, LastModifiedTime, LastAccessTime, MftModifiedTime,
                    IsDirectory, IsHidden, IsReadOnly, IsSystem, IsArchive, IsTemporary, DriveLetter)
                VALUES (@frn, @name, @path, @parent, @size, 0,
                    NULL, NULL, NULL, NULL,
                    @isDir, @hidden, @readOnly, @system, @archive, @temporary, @drive)
                """;
        }
        cmd.Parameters.AddWithValue("@frn", (long)entry.Frn);
        cmd.Parameters.AddWithValue("@name", entry.Name);
        cmd.Parameters.AddWithValue("@path", CompressForStorage(entry.FullPath));
        cmd.Parameters.AddWithValue("@parent", (long)entry.ParentFrn);
        cmd.Parameters.AddWithValue("@size", entry.Size);
        cmd.Parameters.AddWithValue("@isDir", entry.IsDirectory ? 1 : 0);
        cmd.Parameters.AddWithValue("@hidden", entry.IsHidden ? 1 : 0);
        cmd.Parameters.AddWithValue("@readOnly", entry.IsReadOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@system", entry.IsSystem ? 1 : 0);
        cmd.Parameters.AddWithValue("@archive", entry.IsArchive ? 1 : 0);
        cmd.Parameters.AddWithValue("@temporary", entry.IsTemporary ? 1 : 0);
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);
        cmd.ExecuteNonQuery();
    }

    public void DeleteEntry(ulong frn, char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM FileEntries WHERE Frn = @frn AND DriveLetter = @drive";
        cmd.Parameters.AddWithValue("@frn", (long)frn);
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection OpenConnection()
    {
                var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void ClearDriveIndex(char driveLetter)
    {
                using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        ClearDriveIndex(conn, driveLetter);
    }

    public void ClearDriveIndex(SqliteConnection conn, char driveLetter)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM FileEntries WHERE DriveLetter = @drive";
        cmd.Parameters.AddWithValue("@drive", (int)driveLetter);
        cmd.ExecuteNonQuery();
    }

    public void AppendBatch(SqliteConnection conn, IEnumerable<FileEntry> entries, char driveLetter)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO FileEntries 
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

        var frnParam = cmd.Parameters.Add("$frn", SqliteType.Integer);
        var nameParam = cmd.Parameters.Add("$name", SqliteType.Text);
        var pathParam = cmd.Parameters.Add("$path", SqliteType.Blob);
        var parentParam = cmd.Parameters.Add("$parent", SqliteType.Integer);
        var sizeParam = cmd.Parameters.Add("$size", SqliteType.Integer);
        var allocParam = cmd.Parameters.Add("$allocSize", SqliteType.Integer);
        var createdParam = cmd.Parameters.Add("$created", SqliteType.Integer);
        var modifiedParam = cmd.Parameters.Add("$modified", SqliteType.Integer);
        var accessedParam = cmd.Parameters.Add("$accessed", SqliteType.Integer);
        var mftParam = cmd.Parameters.Add("$mftModified", SqliteType.Integer);
        var isDirParam = cmd.Parameters.Add("$isDir", SqliteType.Integer);
        var hiddenParam = cmd.Parameters.Add("$hidden", SqliteType.Integer);
        var readOnlyParam = cmd.Parameters.Add("$readOnly", SqliteType.Integer);
        var systemParam = cmd.Parameters.Add("$system", SqliteType.Integer);
        var archiveParam = cmd.Parameters.Add("$archive", SqliteType.Integer);
        var temporaryParam = cmd.Parameters.Add("$temporary", SqliteType.Integer);
        var driveParam = cmd.Parameters.Add("$drive", SqliteType.Integer);

        foreach (var entry in entries)
        {
            frnParam.Value = (long)entry.Frn;
            nameParam.Value = entry.Name;
            pathParam.Value = CompressForStorage(entry.FullPath);
            parentParam.Value = (long)entry.ParentFrn;
            sizeParam.Value = entry.Size;
            allocParam.Value = entry.AllocatedSize;
            createdParam.Value = (object?)entry.CreationTime?.ToFileTimeUtc() ?? DBNull.Value;
            modifiedParam.Value = (object?)entry.LastModifiedTime?.ToFileTimeUtc() ?? DBNull.Value;
            accessedParam.Value = (object?)entry.LastAccessTime?.ToFileTimeUtc() ?? DBNull.Value;
            mftParam.Value = (object?)entry.MftModifiedTime?.ToFileTimeUtc() ?? DBNull.Value;
            isDirParam.Value = entry.IsDirectory ? 1 : 0;
            hiddenParam.Value = entry.IsHidden ? 1 : 0;
            readOnlyParam.Value = entry.IsReadOnly ? 1 : 0;
            systemParam.Value = entry.IsSystem ? 1 : 0;
            archiveParam.Value = entry.IsArchive ? 1 : 0;
            temporaryParam.Value = entry.IsTemporary ? 1 : 0;
            driveParam.Value = (int)entry.DriveLetter;
            cmd.ExecuteNonQuery();
        }
    }

    public void SetMetadata(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO IndexMetadata (Key, Value) VALUES (@key, @val)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@val", value);
        cmd.ExecuteNonQuery();
    }

    private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;

        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(long))
        {
            long ticks = reader.GetInt64(ordinal);
            if (ticks == 0) return null;
            return DateTime.FromFileTimeUtc(ticks);
        }

        string? value = reader.GetString(ordinal);
        if (string.IsNullOrEmpty(value)) return null;

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;

        return null;
    }

    private static char ReadDriveLetter(SqliteDataReader reader)
    {
        int ordinal = reader.GetOrdinal("DriveLetter");
        if (reader.IsDBNull(ordinal)) return '?';

        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(long))
            return (char)(int)reader.GetInt64(ordinal);

        string s = reader.GetString(ordinal);
        return s.Length > 0 ? s[0] : '?';
    }

    private static string ReadFullPath(SqliteDataReader reader)
    {
        int ordinal = reader.GetOrdinal("FullPath");
        if (reader.IsDBNull(ordinal)) return "";

        if (reader.GetFieldType(ordinal) == typeof(byte[]))
            return DecompressFromStorage((byte[])reader[ordinal]);

        return reader.GetString(ordinal);
    }
}
