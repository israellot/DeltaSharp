using DeltaSharp.Util;

namespace DeltaSharp.Format;

internal static class DeltaWriterBuffer
{
    private const int InitialCapacityLimit = 4096;

    public static MemoryStream Create(int sizeHint) => new(Math.Min(sizeHint, InitialCapacityLimit));

    public static void WriteLiteral(MemoryStream stream, ReadOnlySpan<byte> literal, int trailerBytes)
    {
        // When a large literal sets the capacity, a trailing checksum can immediately double it again.
        if (literal.Length >= InitialCapacityLimit)
        {
            long end = stream.Position + literal.Length;
#if NETSTANDARD2_0
            const int maxCapacity = 0x7FEFFFFF; // Conservative array limit on older runtimes.
#else
            int maxCapacity = Array.MaxLength;
#endif
            if (end >= 2L * stream.Capacity && end <= maxCapacity - trailerBytes)
                stream.Capacity = (int)end + trailerBytes;
        }
        stream.Write(literal);
    }
}
