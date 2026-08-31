using ProjectIndexer.Core.FileSystem;

namespace ProjectIndexer.Core.Tests;

public class FileSystemProviderTests
{
    [Fact]
    public void FileSystemFactory_GetIndexableDrives_ReturnsDrives()
    {
        var drives = FileSystemFactory.GetIndexableDrives();
        Assert.NotNull(drives);
        Assert.IsAssignableFrom<IReadOnlyList<char>>(drives);
    }

    [Fact]
    public void FileSystemFactory_DetectFileSystemType_OnExistingDrive_ReturnsType()
    {
        var type = FileSystemFactory.DetectFileSystemType('C');
        Assert.True(type == FileSystemType.Ntfs || type == FileSystemType.Fat32 || type == FileSystemType.Unknown);
    }

    [Fact]
    public void FileSystemFactory_DetectFileSystemType_OnInvalidDrive_ReturnsUnknown()
    {
        var type = FileSystemFactory.DetectFileSystemType('Z');
        Assert.Equal(FileSystemType.Unknown, type);
    }

    [Fact]
    public void FileSystemFactory_CreateProvider_ForNtfsDrive_ReturnsMftIndexer()
    {
        var ntfsDrives = MftIndexer.GetNtfsDrives();
        if (ntfsDrives.Count == 0) return;

        var provider = FileSystemFactory.CreateProvider(ntfsDrives[0]);
        Assert.IsType<MftIndexer>(provider);
        Assert.Equal(FileSystemType.Ntfs, provider.FileSystemType);
    }

    [Fact]
    public void FileSystemFactory_CreateProvider_ForInvalidDrive_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => FileSystemFactory.CreateProvider('Z'));
    }

    [Fact]
    public void FileSystemFactory_CreateProviderForUnc_InvalidPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => FileSystemFactory.CreateProviderForUnc("C:\\"));
    }

    [Fact]
    public void FileSystemFactory_CreateProviderForUnc_ValidUnc_ReturnsSmbProvider()
    {
        var provider = FileSystemFactory.CreateProviderForUnc(@"\\server\share");
        Assert.IsType<SmbProvider>(provider);
        Assert.Equal(FileSystemType.Smb, provider.FileSystemType);
    }

    [Fact]
    public void MftIndexer_CanProcess_OnInvalidDrive_ReturnsFalse()
    {
        var indexer = new MftIndexer('Z');
        Assert.False(indexer.CanProcess());
    }

    [Fact]
    public void MftIndexer_CanProcess_OnSystemDrive_ReturnsTrue()
    {
        var ntfsDrives = MftIndexer.GetNtfsDrives();
        if (ntfsDrives.Count == 0) return;

        var indexer = new MftIndexer(ntfsDrives[0]);
        Assert.True(indexer.CanProcess());
    }

    [Fact]
    public void FileSystemType_Values_AreDistinct()
    {
        var values = Enum.GetValues<FileSystemType>();
        Assert.Equal(5, values.Length);
        Assert.Contains(FileSystemType.Ntfs, values);
        Assert.Contains(FileSystemType.Fat32, values);
        Assert.Contains(FileSystemType.ExFat, values);
        Assert.Contains(FileSystemType.Smb, values);
        Assert.Contains(FileSystemType.Unknown, values);
    }

    [Fact]
    public void IFileSystemProvider_AllProviders_ImplementInterface()
    {
        Assert.True(typeof(IFileSystemProvider).IsAssignableFrom(typeof(MftIndexer)));
        Assert.True(typeof(IFileSystemProvider).IsAssignableFrom(typeof(FatProvider)));
        Assert.True(typeof(IFileSystemProvider).IsAssignableFrom(typeof(SmbProvider)));
    }
}

public class FatProviderTests
{
    [Fact]
    public void Constructor_SetsDriveLetter()
    {
        var provider = new FatProvider('d');
        Assert.Equal('D', provider.DriveLetter);
        Assert.Equal(FileSystemType.Fat32, provider.FileSystemType);
        Assert.False(provider.SupportsJournaling);
    }

    [Fact]
    public void CanProcess_OnInvalidDrive_ReturnsFalse()
    {
        var provider = new FatProvider('Z');
        Assert.False(provider.CanProcess());
    }

    [Fact]
    public void CanProcess_OnNtfsDrive_ReturnsFalse()
    {
        var ntfsDrives = MftIndexer.GetNtfsDrives();
        if (ntfsDrives.Count == 0) return;

        var provider = new FatProvider(ntfsDrives[0]);
        Assert.False(provider.CanProcess());
    }
}

public class SmbProviderTests
{
    [Fact]
    public void Constructor_ValidUnc_SetsCorrectType()
    {
        var provider = new SmbProvider(@"\\server\share");
        Assert.Equal(FileSystemType.Smb, provider.FileSystemType);
        Assert.False(provider.SupportsJournaling);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("C:\\")]
    [InlineData("local")]
    public void Constructor_InvalidPaths_Throws(string? path)
    {
        if (path == null)
            Assert.Throws<ArgumentException>(() => new SmbProvider(null!));
        else
            Assert.Throws<ArgumentException>(() => new SmbProvider(path));
    }

    [Fact]
    public void CanProcess_OnNonexistentShare_ReturnsFalse()
    {
        var provider = new SmbProvider(@"\\nonexistent-server\nonexistent-share");
        Assert.False(provider.CanProcess());
    }
}
