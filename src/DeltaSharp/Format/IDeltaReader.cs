namespace DeltaSharp.Format;

public interface IDeltaReader
{
    bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed);

}