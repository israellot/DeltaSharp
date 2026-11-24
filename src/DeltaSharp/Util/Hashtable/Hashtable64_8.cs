using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

/// <summary>
/// Stores up to 255 complete 8-byte blocks (source lengths through 2047 bytes).
/// Uses inline buffers on modern targets and byte arrays on .NET Standard 2.0.
/// </summary>
internal readonly ref struct Hashtable64_8
#if NET9_0_OR_GREATER
    : IHashtable
#endif
{
#if NETSTANDARD2_0
    private readonly byte[] _collide = new byte[256];
    private readonly byte[] _landmark = new byte[256];
#else
    [InlineArray(256)]
    struct ByteArray256
    {
        byte Element;
    }

    private readonly ByteArray256 _collide;
    private readonly ByteArray256 _landmark;
#endif

    private const ulong _nHashBitMask = 255;
        
    public Hashtable64_8(ReadOnlySpan<byte> data)
    {
        var nInt = (int)data.Length / 8;

        if (nInt > byte.MaxValue)
            throw new ArgumentException($"Max allowed size is {(byte.MaxValue + 1) * 8 - 1}", nameof(data));

#if !NETSTANDARD2_0
        // Clear whole inline buffers instead of writing each element in a loop.
        _collide = default;
        _landmark = default;
#endif
               
        ReadOnlySpan<ulong> uintData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nInt);


        for (var i = nInt - 1; i >= 0; i -= 1)
        {
            var d = uintData[i];

            var hv = Bucket(d);

            _collide[i] = _landmark[hv];

            _landmark[hv] = (byte)(i + 1);
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Match(ReadOnlySpan<byte> data)
    {
        //we assume data is at least 8 bytes long to do a branchless read
        var ulongData = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(data));

        var bucket = Bucket(ulongData);

        var pos = _landmark[bucket];

        return (pos - 1) * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos)
    {
        var i = (int)((uint)pos / 8);

        return (_collide[i] - 1) * 8;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Bucket(ulong x)
    {
        return (int)(Hash.Hash64(x) & _nHashBitMask);
    }

#if NET9_0_OR_GREATER
    public int FirstCandidate(ReadOnlySpan<byte> data, out int tag, out bool mayMatch)
    {
        tag = 0;
        mayMatch = true;
        return Match(data);
    }

    public int NextCandidate(int pos, int tag, out bool mayMatch)
    {
        mayMatch = true;
        return More(pos);
    }
#endif

    public HashtableStats GetStats()
    {
        var collisions = 256;
        var filled = 256;
        for (var i = 0; i < 256; i++)
        {
            if (_collide[i] == 0) collisions--;
            if (_landmark[i] == 0) filled--;
        }

        return new HashtableStats
        {
            Collisions = collisions,
            Slots = 256,
            SlotsFilled = filled
        };
    }

    public void Dispose()
    {

    }
}
