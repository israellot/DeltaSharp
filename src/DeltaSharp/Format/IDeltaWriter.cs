namespace DeltaSharp.Format;

public interface IDeltaWriter:IDisposable
{
    void WriteLength(ulong length);
    void WriteInsert(ReadOnlySpan<byte> s);
    void WriteCopy(ulong position, ulong length);
    void WriteChecksum(uint checksum);
    ReadOnlyMemory<byte> GetOutput();
}