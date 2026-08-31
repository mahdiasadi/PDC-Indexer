namespace ProjectIndexer.Core.Mft;

internal enum AttributeType : uint
{
    StandardInformation = 0x10,
    AttributeList = 0x20,
    FileName = 0x30,
    ObjectId = 0x40,
    SecurityDescriptor = 0x50,
    VolumeName = 0x60,
    VolumeInformation = 0x70,
    Data = 0x80,
    IndexRoot = 0x90,
    IndexAllocation = 0xA0,
    Bitmap = 0xB0,
    ReparsePoint = 0xC0,
    ExtendedInformation = 0xD0,
    ExtendedAttribute = 0xE0,
    PropertySet = 0xF0,
    End = 0xFFFFFFFF
}

[Flags]
internal enum MftRecordFlags : ushort
{
    None = 0,
    InUse = 0x0001,
    Directory = 0x0002,
    Unknown = 0x0004,
}

internal enum FileNameNamespace : byte
{
    Posix = 0,
    Win32 = 1,
    Dos = 2,
    Win32AndDos = 3,
}

internal static class NtfsConstants
{
    internal const ulong RootDirectoryFrn = 5;
    internal const int BootSectorSize = 512;
    internal const int MftRecordMinSize = 1024;
    internal const int AttributeHeaderSize = 16;
    internal const int ResidentAttributeHeaderSize = 8;
    internal const int FileNameAttributeFixedSize = 0x42;
    internal const int StandardInformationSize = 0x48;
    internal const uint AttributeEnd = 0xFFFFFFFF;
}
