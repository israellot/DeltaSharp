using DeltaSharp.Format;

namespace DeltaSharp;

public interface IDeltaCreate
{
    DeltaResult Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target);
}

public class DeltaCreate<TWriter,TChecksum>: IDeltaCreate
    where TWriter : IDeltaWriter, new()
    where TChecksum : IDeltaChecksum,new()
{
    private const int MinEvaluateLength = 16;


    TChecksum _checksum;

    public DeltaCreate()
    {        
        _checksum = new();
    }

    public DeltaCreate(TChecksum checksum)
    {        
        _checksum = checksum;
    }

    public DeltaResult Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target)
    {
        //using var writer = new TWriter();

        using var writer = (TWriter)(Activator.CreateInstance(typeof(TWriter), origin.Length / 2) ?? new TWriter());

        writer.WriteLength((ulong)target.Length);

        var checksum = _checksum.Checksum(target);

        // If the source is very small, it means that we have no
        // chance of ever doing a copy command.  Just output a single
        // literal segment for the entire target and exit.
        if (target.Length <= MinEvaluateLength)
        {
            writer.WriteInsert(target);
            writer.WriteChecksum(checksum);

            return new DeltaResult()
            {
                Data = writer.GetOutput(),
                Crc32 = checksum
            };

        }

        // Compute the hash table used to locate matching sections in the source.
        using var hashtable = new Hashtable64(origin);
        var blockSize = hashtable.BlockSize();

        var written = ReadOnlySpan<byte>.Empty;
        var unmatched = ReadOnlySpan<byte>.Empty;
        var unwritten = target;
        var next = target;


        while (!next.IsEmpty)
        {
            if(next.Length< blockSize)
                break;

            var bestMatchStart = 0;
            var bestMatchLength = 0;
            var bestMatchedBackwards = 0;

            var p = hashtable.Match(next);
            while (p >= 0)
            {
                var matchedForward = MatchForward(next, origin.Slice(p));
                if (matchedForward > 0)
                {
                    var matchedBackwards = MatchBackwards(unmatched, origin.Slice(0, p));
                    var matchedTotal = matchedForward + matchedBackwards;
                    if (matchedTotal > bestMatchLength)
                    {
                        bestMatchLength = matchedTotal;
                        bestMatchStart = p - matchedBackwards;
                        bestMatchedBackwards = matchedBackwards;

                        if (matchedTotal == unwritten.Length)
                            break; //no need to continue search
                    }
                }
                

                p = hashtable.More(p);
            }

            if (bestMatchLength > 8)
            {
                var insertLength = unmatched.Length - bestMatchedBackwards;
                if (insertLength>0)
                {
                    //insert unmatched bytes
                    writer.WriteInsert(unwritten.Slice(0, insertLength));
                }

                //write copy
                writer.WriteCopy((ulong)bestMatchStart, (ulong)bestMatchLength);

                unwritten = unwritten.Slice(insertLength + bestMatchLength);
                written = written = target.Slice(0, target.Length - unwritten.Length);
                
                next = unwritten;
                unmatched = ReadOnlySpan<byte>.Empty;

            }
            else
            {
                unmatched = unwritten.Slice(0, unmatched.Length + 1);
                next = next.Slice(1);
            }
        }

        if(unwritten.Length>0)
            writer.WriteInsert(unwritten);

        writer.WriteChecksum(checksum);

        return new DeltaResult()
        {
            Data = writer.GetOutput(),
            Crc32 = checksum
        };
    }

    public int MatchForward(ReadOnlySpan<byte> x,ReadOnlySpan<byte> y)
    {
        var count = 0;
        while(count<x.Length && count<y.Length)
        {
            if (x[count] != y[count])
                break;

            count++;
        }

        return count;
    }

    public int MatchBackwards(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var i = 1;
        while (i <= x.Length && i <= y.Length)
        {
            if (x[x.Length-i] != y[y.Length-i])
                break;

            i++;
        }

        return i - 1;
    }

}

public class DeltaResult
{
    public ReadOnlyMemory<byte> Data { get; set; }

    public uint Crc32 { get; set; }

}


public class DeltaCreateException : Exception
{
    public DeltaCreateExceptionError Error { get; private set; }

    public DeltaCreateException(DeltaCreateExceptionError error)
    {
         
    }

    public enum DeltaCreateExceptionError
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

