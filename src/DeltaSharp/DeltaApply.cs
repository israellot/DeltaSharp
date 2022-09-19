using DeltaSharp.Format;
using static DeltaSharp.DeltaApplyException;

namespace DeltaSharp;

public interface IDeltaApply
{

    ReadOnlyMemory<byte> Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta);

}

public class DeltaApply<TReader,TChecksum> : IDeltaApply
    where TReader : IDeltaReader, new()
    where TChecksum : IDeltaChecksum,new()
{
    TReader _reader;
    TChecksum _checksum;

    public DeltaApply()
    {
        _reader = new();
        _checksum = new();
    }

    public DeltaApply(TReader reader, TChecksum checksum)
    {
        _reader = reader;
        _checksum = checksum;
    }

    public ulong GetOutputLength(ReadOnlySpan<byte> delta)
    {
        

        if (!_reader.TryRead(delta, out var cmd, out var consumed))
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

        if (cmd.Command != DeltaCommandEnum.Length)
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

        return cmd.Length;
    }

    public ReadOnlyMemory<byte> Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        var outputLength = GetOutputLength(delta);

        if(outputLength>int.MaxValue)
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidOutputLength);

        var buffer = new byte[(int)outputLength];

        var output= Apply(source, delta, buffer);

        return buffer;
    }

    public Span<byte> Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, Span<byte> outputBuffer)
    {
        var outputSpan = outputBuffer;
        var writeSpan = outputSpan;

        while (_reader.TryRead(delta,out var cmd,out var consumed))
        {
            switch (cmd.Command)
            {
                case DeltaCommandEnum.Length:
                    {                        
                        if (outputBuffer.Length < (int)cmd.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.OutputMemorySizeLessThanRequired);

                        writeSpan = writeSpan.Slice(0, (int)cmd.Length);

                        delta = delta.Slice((int)consumed);

                        break;
                    }
                case DeltaCommandEnum.Insert:
                    {
                        if (cmd.Length > (ulong)writeSpan.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidInsertLength);

                        delta = delta.Slice((int)consumed);

                        var insertSlice = delta.Slice(0,(int)cmd.Length);

                        insertSlice.CopyTo(writeSpan);

                        delta = delta.Slice((int)cmd.Length);

                        writeSpan = writeSpan.Slice((int)cmd.Length);

                        break;
                    }
                case DeltaCommandEnum.Copy:
                    {
                        if (cmd.Position > (ulong)source.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidCopyPosition);

                        if (cmd.Length > (ulong)writeSpan.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidCopyLength);

                        delta = delta.Slice((int)consumed);

                        var copySlice = source.Slice((int)cmd.Position, (int)cmd.Length);
                        copySlice.CopyTo(writeSpan);

                        writeSpan = writeSpan.Slice((int)cmd.Length);

                        break;
                    }
                case DeltaCommandEnum.Checksum:
                    {
                        delta = delta.Slice((int)consumed);

                        var deltaChecksum = cmd.Length;

                        var outputChecksum = _checksum.Checksum(outputSpan);

                        if (deltaChecksum != outputChecksum)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidChecksum);

                        return outputSpan;

                    }
            }
        }

        return outputSpan;
    }

}

public class DeltaApplyException : Exception
{
    public DeltaApplyExceptionError Error { get; private set; }

    public DeltaApplyException(DeltaApplyExceptionError error)
    {
         
    }

    public enum DeltaApplyExceptionError
    {
        InvalidDelta,
        InvalidOutputLength,
        OutputMemorySizeLessThanRequired,
        InvalidInsertLength,
        InvalidCopyPosition,
        InvalidCopyLength,
        InvalidChecksum,

    }
}
