namespace ProjectIndexer.Core.Mft;

internal class BootSector
{
    public ushort BytesPerSector { get; private set; }
    public byte SectorsPerCluster { get; private set; }
    public int BytesPerCluster => BytesPerSector * SectorsPerCluster;
    public ulong MftStartCluster { get; private set; }
    public ulong MftMirrorStartCluster { get; private set; }
    public int MftRecordSize { get; private set; }
    public int ClustersPerIndexRecord { get; private set; }
    public int IndexRecordSize { get; private set; }
    public ulong TotalSectors { get; private set; }
    public ulong MftZoneSize { get; private set; }
    public byte MediaType { get; private set; }
    public byte[] RawData { get; private set; } = [];

    public static BootSector Parse(byte[] data)
    {
        if (data.Length < NtfsConstants.BootSectorSize)
            throw new InvalidOperationException("Boot sector data is too short");

        if (data[0] != 0xEB || data[3] != 0x4E || data[4] != 0x54 || data[5] != 0x46 || data[6] != 0x53)
            throw new InvalidOperationException("Not a valid NTFS volume (missing NTFS signature)");

        var bs = new BootSector
        {
            RawData = data,
            BytesPerSector = BitConverter.ToUInt16(data, 0x0B),
            SectorsPerCluster = data[0x0D],
            MediaType = data[0x15],
            TotalSectors = BitConverter.ToUInt64(data, 0x28),
            MftStartCluster = BitConverter.ToUInt64(data, 0x30),
            MftMirrorStartCluster = BitConverter.ToUInt64(data, 0x38),
            ClustersPerIndexRecord = data[0x44],
        };

        sbyte mftRecordSizeLog2 = (sbyte)data[0x40];
        bs.MftRecordSize = mftRecordSizeLog2 < 0
            ? 1 << (-mftRecordSizeLog2)
            : bs.BytesPerCluster * mftRecordSizeLog2;

        sbyte indexRecordSizeLog2 = (sbyte)data[0x44];
        bs.IndexRecordSize = indexRecordSizeLog2 < 0
            ? 1 << (-indexRecordSizeLog2)
            : bs.BytesPerCluster * indexRecordSizeLog2;

        bs.MftZoneSize = BitConverter.ToUInt32(data, 0x48);

        return bs;
    }

    public long MftByteOffset => (long)((long)MftStartCluster * BytesPerCluster);
}
