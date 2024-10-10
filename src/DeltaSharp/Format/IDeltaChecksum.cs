using System.Buffers.Binary;

namespace DeltaSharp.Format;

public interface IDeltaChecksum
{
    uint Checksum(ReadOnlySpan<byte> data); 

}
