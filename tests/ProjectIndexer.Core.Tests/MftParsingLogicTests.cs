using System.Runtime.InteropServices;
using ProjectIndexer.Core.Mft;
using ProjectIndexer.Core.Models;

namespace ProjectIndexer.Core.Tests;

public class MftParsingLogicTests
{
    [Fact]
    public void BootSector_Parse_ValidNtfsData_ReturnsCorrectValues()
    {
        var data = CreateBootSector(bytesPerSector: 512, sectorsPerCluster: 8, mftCluster: 0x123456, mftRecordSizeLog2: -10);
        var bootSector = BootSector.Parse(data);

        Assert.Equal(512, bootSector.BytesPerSector);
        Assert.Equal(8, bootSector.SectorsPerCluster);
        Assert.Equal(4096, bootSector.BytesPerCluster);
        Assert.Equal(0x123456UL, bootSector.MftStartCluster);
        Assert.Equal(1024, bootSector.MftRecordSize);
    }

    [Fact]
    public void BootSector_Parse_LargeRecordSize_CalculatedCorrectly()
    {
        var data = CreateBootSector(mftRecordSizeLog2: -15);
        var bootSector = BootSector.Parse(data);

        Assert.Equal(32768, bootSector.MftRecordSize);
    }

    [Fact]
    public void BootSector_Parse_InvalidData_ThrowsException()
    {
        var data = new byte[512];
        Assert.Throws<InvalidOperationException>(() => BootSector.Parse(data));
    }

    [Fact]
    public void MftRecordHeader_Parse_InUseRecord_ReturnsCorrectFlags()
    {
        var record = CreateMftRecord(flags: 0x01, recordNumber: 42);
        var header = MftRecordHeader.Parse(new Span<byte>(record), 42);

        Assert.True(header.IsInUse);
        Assert.False(header.IsDirectory);
        Assert.Equal(42, header.RecordNumber);
    }

    [Fact]
    public void MftRecordHeader_Parse_DirectoryRecord_ReturnsDirectoryFlag()
    {
        var record = CreateMftRecord(flags: 0x03, recordNumber: 100);
        var header = MftRecordHeader.Parse(new Span<byte>(record), 100);

        Assert.True(header.IsInUse);
        Assert.True(header.IsDirectory);
    }

    [Fact]
    public void MftRecordHeader_Parse_WithBaseRecord_HasBaseRecordTrue()
    {
        var record = CreateMftRecord(flags: 0x01, recordNumber: 50, baseRecord: 10);
        var header = MftRecordHeader.Parse(new Span<byte>(record), 50);

        Assert.True(header.HasBaseRecord);
        Assert.Equal(10UL, header.BaseRecordReference);
    }

    [Fact]
    public void MftRecordHeader_FixupApplication_RestoresCorrectData()
    {
        var record = CreateMftRecordWithFixups();
        int recordSize = BitConverter.ToInt32(record[24..]);

        var header = MftRecordHeader.Parse(new Span<byte>(record), 0);
        MftRecordHeader.ApplyFixups(new Span<byte>(record), header.FixupOffset, header.FixupCount, 512);

        Assert.Equal(header.FixupCount - 1, CountCorrectFixups(record, header.FixupOffset, header.FixupCount, 512));
    }

    [Fact]
    public void FileEntry_Properties_WorkCorrectly()
    {
        var entry = new FileEntry
        {
            Frn = 1000,
            Name = "test.txt",
            FullPath = @"C:\Users\test.txt",
            ParentFrn = 500,
            Size = 1024,
            IsDirectory = false,
            IsHidden = false,
            DriveLetter = 'C'
        };

        Assert.Equal(1000UL, entry.Frn);
        Assert.Equal("test.txt", entry.Name);
        Assert.Equal(@"C:\Users\test.txt", entry.FullPath);
        Assert.Equal("txt", entry.NameExtension);
        Assert.Equal('C', entry.DriveLetter);
    }

    [Fact]
    public void FileEntry_NameExtension_WithNoExtension_ReturnsEmpty()
    {
        var entry = new FileEntry { Name = "README" };
        Assert.Equal("", entry.NameExtension);
    }

    [Fact]
    public void FileEntry_NameExtension_WithMultipleDots_UsesLast()
    {
        var entry = new FileEntry { Name = "archive.tar.gz" };
        Assert.Equal("gz", entry.NameExtension);
    }

    private static byte[] CreateBootSector(
        int bytesPerSector = 512,
        int sectorsPerCluster = 8,
        int mftRecordSizeLog2 = 0x0A,
        ulong mftCluster = 0x100000)
    {
        var data = new byte[512];
        data[0] = 0xEB;
        data[1] = 0x52;
        data[2] = 0x90;
        data[3] = 0x4E;
        data[4] = 0x54;
        data[5] = 0x46;
        data[6] = 0x53;
        data[7] = 0x20;
        data[8] = 0x20;
        data[9] = 0x20;
        data[10] = 0x20;

        BitConverter.GetBytes((ushort)bytesPerSector).CopyTo(data, 0x0B);
        data[0x0D] = (byte)sectorsPerCluster;

        BitConverter.GetBytes((ulong)1000000).CopyTo(data, 0x28);

        BitConverter.GetBytes(mftCluster).CopyTo(data, 0x30);
        BitConverter.GetBytes(mftCluster).CopyTo(data, 0x38);

        data[0x40] = (byte)mftRecordSizeLog2;
        data[0x44] = 0x01;

        return data;
    }

    private static byte[] CreateMftRecord(
        ushort flags = 0x01,
        int recordNumber = 0,
        ulong baseRecord = 0,
        int recordSize = 1024)
    {
        var data = new byte[recordSize];

        BitConverter.GetBytes(0x454C4946u).CopyTo(data, 0);

        ushort fixupOffset = 48;
        ushort fixupCount = (ushort)(recordSize / 512 + 1);
        BitConverter.GetBytes(fixupOffset).CopyTo(data, 4);
        BitConverter.GetBytes(fixupCount).CopyTo(data, 6);

        BitConverter.GetBytes((ushort)1).CopyTo(data, 16);
        BitConverter.GetBytes((ushort)1).CopyTo(data, 18);

        ushort attrOffset = (ushort)(fixupOffset + fixupCount * 2);
        BitConverter.GetBytes(attrOffset).CopyTo(data, 20);

        BitConverter.GetBytes(flags).CopyTo(data, 22);
        BitConverter.GetBytes(recordSize).CopyTo(data, 24);
        BitConverter.GetBytes(recordSize).CopyTo(data, 28);
        BitConverter.GetBytes(baseRecord).CopyTo(data, 32);
        BitConverter.GetBytes((ushort)1).CopyTo(data, 40);

        for (int i = 0; i < fixupCount - 1; i++)
        {
            int sectorEnd = (i + 1) * 512 - 2;
            int fixupOffset2 = fixupOffset + (i + 1) * 2;
            ushort fixupValue = (ushort)(sectorEnd ^ 0xAAAA);
            BitConverter.GetBytes(fixupValue).CopyTo(data, fixupOffset2);
            BitConverter.GetBytes((ushort)0xAAAA).CopyTo(data, sectorEnd);
        }

        return data;
    }

    private static byte[] CreateMftRecordWithFixups(int recordSize = 2048)
    {
        var rawRecord = CreateMftRecord(flags: 0x01, recordSize: recordSize);
        int fixupOffset = BitConverter.ToUInt16(rawRecord, 4);
        int fixupCount = BitConverter.ToUInt16(rawRecord, 6);

        var data = new byte[recordSize];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i ^ 0xFF);

        if (fixupOffset + fixupCount * 2 > data.Length)
            fixupCount = (data.Length - fixupOffset) / 2;

        BitConverter.GetBytes(0x454C4946u).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)fixupOffset).CopyTo(data, 4);
        BitConverter.GetBytes((ushort)fixupCount).CopyTo(data, 6);

        ushort usaValue = 0xABCD;
        BitConverter.GetBytes(usaValue).CopyTo(data, fixupOffset);

        for (int i = 1; i < fixupCount; i++)
        {
            int sectorEnd = i * 512 - 2;
            if (sectorEnd < 0 || sectorEnd + 1 >= data.Length) break;
            ushort originalValue = BitConverter.ToUInt16(data, sectorEnd);
            int fixupEntry = fixupOffset + i * 2;
            if (fixupEntry + 1 >= data.Length) break;
            BitConverter.GetBytes(originalValue).CopyTo(data, fixupEntry);
            BitConverter.GetBytes(usaValue).CopyTo(data, sectorEnd);
        }

        return data;
    }

    private static int CountCorrectFixups(byte[] record, int fixupOffset, int fixupCount, int bytesPerSector)
    {
        int correct = 0;
        for (int i = 1; i < fixupCount; i++)
        {
            ushort fixupValue = BitConverter.ToUInt16(record, fixupOffset + i * 2);
            int sectorEnd = i * bytesPerSector - 2;
            ushort sectorValue = BitConverter.ToUInt16(record, sectorEnd);
            if (fixupValue == sectorValue) correct++;
        }
        return correct;
    }
}
