namespace ProjectIndexer.Core.Mft;

internal ref struct MftRecordHeader
{
    public uint Signature { get; private set; }
    public ushort FixupOffset { get; private set; }
    public ushort FixupCount { get; private set; }
    public ushort SequenceNumber { get; private set; }
    public ushort LinkCount { get; private set; }
    public ushort AttributeOffset { get; private set; }
    public MftRecordFlags Flags { get; private set; }
    public int RecordSize { get; private set; }
    public int AllocatedSize { get; private set; }
    public ulong BaseRecordReference { get; private set; }
    public ushort NextAttributeId { get; private set; }
    public bool IsInUse => (Flags & MftRecordFlags.InUse) != 0;
    public bool IsDirectory => (Flags & MftRecordFlags.Directory) != 0;
    public bool HasBaseRecord => BaseRecordReference != 0;
    public int RecordNumber { get; set; }

    public static MftRecordHeader Parse(ReadOnlySpan<byte> data, int recordNumber)
    {
        if (data.Length < 48)
            throw new InvalidOperationException("Record data too short for header");

        var header = new MftRecordHeader
        {
            Signature = BitConverter.ToUInt32(data),
            FixupOffset = BitConverter.ToUInt16(data[4..]),
            FixupCount = BitConverter.ToUInt16(data[6..]),
            SequenceNumber = BitConverter.ToUInt16(data[16..]),
            LinkCount = BitConverter.ToUInt16(data[18..]),
            AttributeOffset = BitConverter.ToUInt16(data[20..]),
            Flags = (MftRecordFlags)BitConverter.ToUInt16(data[22..]),
            RecordSize = BitConverter.ToInt32(data[24..]),
            AllocatedSize = BitConverter.ToInt32(data[28..]),
            BaseRecordReference = BitConverter.ToUInt64(data[32..]),
            NextAttributeId = BitConverter.ToUInt16(data[40..]),
            RecordNumber = recordNumber,
        };

        return header;
    }

    public static void ApplyFixups(Span<byte> record, int fixupOffset, int fixupCount, int bytesPerSector)
    {
        if (fixupOffset == 0 || fixupCount == 0) return;

        ushort updateSequenceNumber = BitConverter.ToUInt16(record[fixupOffset..]);

        for (int i = 1; i < fixupCount; i++)
        {
            ushort fixupValue = BitConverter.ToUInt16(record[(fixupOffset + i * 2)..]);
            int sectorEnd = i * bytesPerSector - 2;

            if (sectorEnd < record.Length)
            {
                record[sectorEnd] = (byte)(fixupValue & 0xFF);
                record[sectorEnd + 1] = (byte)((fixupValue >> 8) & 0xFF);
            }
        }
    }
}
