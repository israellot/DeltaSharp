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

/// <summary>Creates patches that reconstruct a target from an original source.</summary>
public interface IDeltaCreate
{
    /// <summary>Creates a patch using this encoder's format, checksum algorithm, and search settings.</summary>
    /// <param name="origin">The original source bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <returns>The encoded patch and the checksum of the target, rather than of the patch bytes.</returns>
    /// <remarks>The patch must be applied with the same format and checksum algorithm. Inputs are not modified.</remarks>
    DeltaResult Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target);
}

/// <summary>Creates patches using a configurable format writer and target checksum algorithm.</summary>
/// <typeparam name="TWriter">The patch writer, with a public parameterless constructor.</typeparam>
/// <typeparam name="TChecksum">The target checksum algorithm, with a public parameterless constructor.</typeparam>
/// <remarks>
/// Each call creates and disposes a writer. If the writer has a public constructor taking a single
/// <see cref="int"/>, it receives an initial capacity hint in bytes. Output returned by the writer must
/// remain valid after disposal. The checksum instance is reused, so its implementation determines
/// whether concurrent calls are safe. Source-match search does not guarantee a minimum-size patch.
/// </remarks>
public class DeltaCreate<TWriter,TChecksum>: IDeltaCreate
    where TWriter : IDeltaWriter, new()
    where TChecksum : IDeltaChecksum,new()
{
    private const int MinEvaluateLength = 16;
    private const int BoundedCandidateLimit = 256;
    private static readonly System.Reflection.ConstructorInfo? SizeHintConstructor =
        typeof(TWriter).GetConstructor(new[] { typeof(int) });


    private TChecksum _checksum;
    private readonly int _maxCandidatesPerPosition;

    /// <summary>Initializes an encoder with a new checksum instance and exhaustive source-match search.</summary>
    public DeltaCreate() : this(new TChecksum(), DeltaSearchMode.Exhaustive)
    {
    }

    /// <summary>Initializes an encoder with the supplied checksum instance and exhaustive source-match search.</summary>
    /// <param name="checksum">The non-null checksum implementation to retain for subsequent calls.</param>
    public DeltaCreate(TChecksum checksum) : this(checksum, DeltaSearchMode.Exhaustive)
    {
    }

    /// <summary>Initializes an encoder with the supplied checksum instance and default source-match search mode.</summary>
    /// <param name="checksum">The non-null checksum implementation to retain for subsequent calls.</param>
    /// <param name="searchMode">The search-effort policy used by calls without an explicit mode.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public DeltaCreate(TChecksum checksum, DeltaSearchMode searchMode)
    {
        _maxCandidatesPerPosition = GetCandidateLimit(searchMode);
        _checksum = checksum;
    }

    /// <inheritdoc cref="IDeltaCreate.Create"/>
    public DeltaResult Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target)
        => CreateCore(origin, target, _maxCandidatesPerPosition);

    /// <summary>Creates a patch using the selected search mode for this call only.</summary>
    /// <param name="origin">The original source bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <param name="searchMode">The search-effort policy for this call; does not change the encoder's default.</param>
    /// <returns>The encoded patch and the checksum of the target, rather than of the patch bytes.</returns>
    /// <remarks>The patch must be applied with the same format and checksum algorithm. Inputs are not modified.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public DeltaResult Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => CreateCore(origin, target, GetCandidateLimit(searchMode));

    private static int GetCandidateLimit(DeltaSearchMode searchMode) => searchMode switch
    {
        // Source spans cannot contain enough eight-byte blocks to reach this limit.
        DeltaSearchMode.Exhaustive => int.MaxValue,
        DeltaSearchMode.Bounded => BoundedCandidateLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(searchMode), searchMode, "Unknown search mode.")
    };

    private DeltaResult CreateCore(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target, int maxCandidatesPerPosition)
    {
        var writer = SizeHintConstructor is null
            ? new TWriter()
            : (TWriter)SizeHintConstructor.Invoke(new object[] { Math.Min(origin.Length / 2, target.Length) });

        try
        {
            writer.WriteLength((ulong)target.Length);
            var checksum = _checksum.Checksum(target);

            if (target.Length > MinEvaluateLength)
                WriteMatches(origin, target, maxCandidatesPerPosition, ref writer);
            else if (!target.IsEmpty)
                writer.WriteInsert(target);

            writer.WriteChecksum(checksum);
            return new DeltaResult { Data = writer.GetOutput(), Crc32 = checksum };
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void WriteMatches(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target,
        int maxCandidatesPerPosition, ref TWriter writer)
    {
#if NET9_0_OR_GREATER
        switch (Hashtable64.GetIndexWidth(origin.Length))
        {
            case 8:
                using (var table = new Hashtable64_8(origin))
                    EncodeMatches(table, origin, target, maxCandidatesPerPosition, ref writer);
                break;
            case 16:
                using (var table = new Hashtable64_16(origin))
                    EncodeMatches(table, origin, target, maxCandidatesPerPosition, ref writer);
                break;
#if DELTASHARP_HASHTABLE24
            case 24:
                using (var table = new Hashtable64_24(origin))
                    EncodeMatches(table, origin, target, maxCandidatesPerPosition, ref writer);
                break;
#endif
            default:
                using (var table = new Hashtable64_32(origin))
                    EncodeMatches(table, origin, target, maxCandidatesPerPosition, ref writer);
                break;
        }
#else
        using var table = new Hashtable64(origin);
        EncodeMatches(table, origin, target, maxCandidatesPerPosition, ref writer);
#endif
    }

#if NET9_0_OR_GREATER
    private static void EncodeMatches<TTable>(TTable hashtable, ReadOnlySpan<byte> origin,
        ReadOnlySpan<byte> target, int maxCandidatesPerPosition, ref TWriter writer)
        where TTable : IHashtable, allows ref struct
#else
    private static void EncodeMatches(Hashtable64 hashtable, ReadOnlySpan<byte> origin,
        ReadOnlySpan<byte> target, int maxCandidatesPerPosition, ref TWriter writer)
#endif
    {
        var unmatched = ReadOnlySpan<byte>.Empty;
        var unwritten = target;
        var next = target;

        while (!next.IsEmpty)
        {
            if (next.Length < Hashtable64.BytesPerBlock)
                break;

            var bestMatchStart = 0;
            var bestMatchLength = 0;
            var bestMatchedBackwards = 0;

            var firstByte = next[0];
            var p = hashtable.Match(next);
            var candidatesRemaining = maxCandidatesPerPosition;
            while (p >= 0 && candidatesRemaining-- > 0)
            {
                // A hash collision can still extend backwards if even one byte matches forwards.
                if (origin[p] == firstByte)
                {
                    var matchedForward = SpanHelper.MatchForward(next, origin.Slice(p));
                    var matchedBackwards = !unmatched.IsEmpty && p > 0 && unmatched[unmatched.Length - 1] == origin[p - 1]
                        ? SpanHelper.MatchBackward(unmatched, origin.Slice(0, p))
                        : 0;
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

        if (unwritten.Length > 0)
            writer.WriteInsert(unwritten);
    }
}

/// <summary>Contains the bytes produced by creating or applying a patch and the target checksum.</summary>
public class DeltaResult
{
    /// <summary>Gets or sets the encoded patch after creation, or the reconstructed target after application.</summary>
    /// <remarks>
    /// A read-only view does not make the backing storage immutable. When applying into caller-provided
    /// memory, this references the written portion of that memory. Built-in allocating overloads return
    /// newly allocated storage; custom writers determine the storage lifetime of their creation results.
    /// </remarks>
    public ReadOnlyMemory<byte> Data { get; set; }

    /// <summary>Gets or sets the checksum of the reconstructed target, as computed by the configured checksum implementation.</summary>
    /// <remarks>
    /// The built-in formats use the Fossil additive checksum, not CRC-32. The property retains its
    /// historical name; custom checksum implementations may use a different algorithm.
    /// </remarks>
    public uint Crc32 { get; set; }

}


/// <summary>Represents a delta creation failure using the legacy creation error codes.</summary>
/// <remarks>
/// Retained for API compatibility. The built-in encoder currently does not throw this type;
/// invalid search modes throw <see cref="ArgumentOutOfRangeException"/> and writer or checksum failures propagate.
/// </remarks>
public class DeltaCreateException : Exception
{
    /// <summary>Gets the creation error code supplied to the constructor.</summary>
    public DeltaCreateExceptionError Error { get; private set; }

    /// <summary>Initializes an exception with the specified legacy creation error code.</summary>
    /// <param name="error">The error code to report.</param>
    public DeltaCreateException(DeltaCreateExceptionError error)
    {
        Error = error;
    }

    /// <summary>Defines legacy creation error codes retained for API compatibility.</summary>
    public enum DeltaCreateExceptionError
    {
        /// <summary>Identifies an invalid delta representation.</summary>
        InvalidDelta,
        /// <summary>Identifies an invalid output length.</summary>
        InvalidOutputLength,
        /// <summary>Identifies output memory smaller than the required length.</summary>
        OutputMemorySizeLessThanRequired,
        /// <summary>Identifies an invalid literal insertion length.</summary>
        InvalidInsertLength,
        /// <summary>Identifies an invalid source position for a copy.</summary>
        InvalidCopyPosition,
        /// <summary>Identifies an invalid source copy length.</summary>
        InvalidCopyLength,
        /// <summary>Identifies an invalid checksum.</summary>
        InvalidChecksum,

    }
}

