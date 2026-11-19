using DeltaSharp.Format;
using DeltaSharp.Util;
using DeltaSharp.Util.Hashtable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    private static readonly System.Reflection.ConstructorInfo? SizeHintConstructor =
        typeof(TWriter).GetConstructor(new[] { typeof(int) });


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
        using var writer = SizeHintConstructor is null
            ? new TWriter()
            : (TWriter)SizeHintConstructor.Invoke(new object[] { Math.Min(origin.Length / 2, target.Length) });

        writer.WriteLength((ulong)target.Length);

        var checksum = _checksum.Checksum(target);

        // Small targets are inexpensive to encode as a single literal.
        if (target.Length <= MinEvaluateLength)
        {
            if (!target.IsEmpty)
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
        var blockSize = hashtable.BlockSize;

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
                //target matched at landmark on origin

                //actually match forward as long as possible
                var matchedForward = SpanHelper.MatchForward(next, origin.Slice(p));
                if (matchedForward > 0)
                {
                    //check if we can match backwards too
                    var matchedBackwards = unmatched.Length > 0 ? SpanHelper.MatchBackward(unmatched, origin.Slice(0, p)) : 0;
                    var matchedTotal = matchedForward + matchedBackwards;
                    if (matchedTotal > bestMatchLength)
                    {
                        //this match is the new best so far
                        bestMatchLength = matchedTotal;
                        bestMatchStart = p - matchedBackwards;
                        bestMatchedBackwards = matchedBackwards;

                        if (matchedTotal == unwritten.Length)
                            break; //no need to continue search, we matched the whole target
                    }
                }
                

                p = hashtable.More(p);
            }

            if (bestMatchLength > 8)
            {
                //a sensible limit for a copy command
                //short matches may end up being longer than insert commands when encoding

                var insertLength = unmatched.Length - bestMatchedBackwards;
                if (insertLength>0)
                {
                    //insert unmatched bytes
                    writer.WriteInsert(unwritten.Slice(0, insertLength));
                }

                //write copy
                writer.WriteCopy((ulong)bestMatchStart, (ulong)bestMatchLength);

                unwritten = unwritten.Slice(insertLength + bestMatchLength);
                
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

}

public class DeltaResult
{
    public ReadOnlyMemory<byte> Data { get; set; }

    /// <summary>The Fossil additive checksum, retained under its historical name; not a CRC-32.</summary>
    public uint Crc32 { get; set; }

}


public class DeltaCreateException : Exception
{
    public DeltaCreateExceptionError Error { get; private set; }

    public DeltaCreateException(DeltaCreateExceptionError error)
    {
        Error = error;
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

