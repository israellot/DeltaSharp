using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

/// <summary>
/// Specialized hashtable to store landmarks and respective collisions
/// Because we hash at every 8 bytes, we can store the address as multiple of 8
/// To improve memory usage, there are specialized implementations according to data size
/// Byte-sized indices can address 255 complete 8-byte blocks, including sources
/// up to 2047 bytes long with a partial final block.
/// </summary>
internal readonly ref struct Hashtable64
{
    internal const int BytesPerBlock = 8;
    public int BlockSize => BytesPerBlock;

    private readonly Hashtable64_8 _hashtable8;
    private readonly Hashtable64_16 _hashtable16;
#if DELTASHARP_HASHTABLE24
    private readonly Hashtable64_24 _hashtable24;
#endif
    private readonly Hashtable64_32 _hashtable32;
    private readonly byte _hashtable;

    internal static byte GetIndexWidth(int sourceLength)
    {
        // Zero is the empty-slot sentinel; stored block indices are one-based.
        if (sourceLength / BytesPerBlock <= byte.MaxValue)
            return 8;
        if (sourceLength < ushort.MaxValue * BytesPerBlock)
            return 16;
#if DELTASHARP_HASHTABLE24
        if (sourceLength < 0xff_ff_ff * BytesPerBlock)
            return 24;
#endif
        return 32;
    }

    public Hashtable64(ReadOnlySpan<byte> data)
    {
        _hashtable = GetIndexWidth(data.Length);
        switch (_hashtable)
        {
            case 8: _hashtable8 = new Hashtable64_8(data); break;
            case 16: _hashtable16 = new Hashtable64_16(data); break;
#if DELTASHARP_HASHTABLE24
            case 24: _hashtable24 = new Hashtable64_24(data); break;
#endif
            default: _hashtable32 = new Hashtable64_32(data); break;
        }
    }

    public int Match(ReadOnlySpan<byte> data)
    {
        switch (_hashtable)
        {
            case 8: return _hashtable8.Match(data);
            case 16: return _hashtable16.Match(data);
#if DELTASHARP_HASHTABLE24
            case 24: return _hashtable24.Match(data);
#endif
            default: return _hashtable32.Match(data);
        }
    }

    public int More(int pos)
    {
        switch (_hashtable)
        {
            case 8: return _hashtable8.More(pos);
            case 16: return _hashtable16.More(pos);
#if DELTASHARP_HASHTABLE24
            case 24: return _hashtable24.More(pos);
#endif
            default: return _hashtable32.More(pos);
        }
    }

#if !NET9_0_OR_GREATER
    public bool UsesFirstByteTag => _hashtable == 32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstCandidate<TMode>(ReadOnlySpan<byte> data, out int tag, out bool mayMatch)
        where TMode : struct, ICandidateMode
    {
        if (default(TMode).UsesFirstByteTag)
            return _hashtable32.FirstCandidate(data, out tag, out mayMatch);

        tag = 0;
        mayMatch = true;
        return Match(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextCandidate<TMode>(int pos, int tag, out bool mayMatch)
        where TMode : struct, ICandidateMode
    {
        if (default(TMode).UsesFirstByteTag)
            return _hashtable32.NextCandidate(pos, tag, out mayMatch);

        mayMatch = true;
        return More(pos);
    }
#endif

    public void Dispose()
    {
        switch (_hashtable)
        {
            case 8: _hashtable8.Dispose(); break;
            case 16: _hashtable16.Dispose(); break;
#if DELTASHARP_HASHTABLE24
            case 24: _hashtable24.Dispose(); break;
#endif
            default: _hashtable32.Dispose(); break;
        }
    }
}

#if !NET9_0_OR_GREATER
// Make the portable encoder's tag decision once per Create. The JIT specializes
// its candidate loop for each of these value types.
internal interface ICandidateMode
{
    bool UsesFirstByteTag { get; }
}

internal readonly struct TaggedCandidates : ICandidateMode
{
    public bool UsesFirstByteTag => true;
}

internal readonly struct UntaggedCandidates : ICandidateMode
{
    public bool UsesFirstByteTag => false;
}
#endif
