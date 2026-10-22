namespace LegacyForgePatcher.ClassFiles;

/// <summary>Writes the big-endian integers of class files.</summary>
internal static class StreamExtensions
{
    public static void WriteU2(this Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    public static void WriteU4(this Stream stream, uint value)
    {
        stream.WriteU2((ushort)(value >> 16));
        stream.WriteU2((ushort)value);
    }
}
