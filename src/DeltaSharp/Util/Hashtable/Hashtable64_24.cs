#if DELTASHARP_HASHTABLE24
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

/// <summary>
/// Stores up to 16,777,215 complete 8-byte blocks (source lengths through 134,217,727 bytes)
/// using packed 24-bit, one-based block indices in a pooled byte array.
/// Reads mask a 32-bit value to 24 bits; writes use one byte and one 16-bit value
/// so neighboring entries remain unchanged.
/// </summary>
internal unsafe readonly ref struct Hashtable64_24
#if NET9_0_OR_GREATER
    : IHashtable
#endif
{

    private readonly ReadOnlySpan<byte> _collide;

    private readonly ReadOnlySpan<byte> _landmark;

    private readonly ulong _nHashBitMask;

    private readonly byte[] _buffer;

    public Hashtable64_24(ReadOnlySpan<byte> data)
    {
        var nLong = (int)data.Length / 8;

        if (nLong > 0x00_ff_ff_ff)
            throw new ArgumentException($"Max allowed size is {0x00_ff_ff_ff * 8}", nameof(data));

        // Compute the hash table used to locate matching sections in the source.
        int nHash = Hash.TableSize(nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        var arraySize = (nHash * 3 + 1); //we need one extra byte to avoid special casing of the last element

        _buffer = ArrayPool<byte>.Shared.Rent(arraySize * 2);        
        _buffer.AsSpan().Slice(0,arraySize*2).Fill(0);

        Span<byte> collide = _buffer.AsSpan(0, arraySize);
        Span<byte> landmark = _buffer.AsSpan(arraySize, arraySize);

        ReadOnlySpan<ulong> ulongData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nLong);

        fixed (byte* collideP = collide)
        fixed (byte* landmarkP = landmark)
        {
            for (var i = nLong - 1; i >= 0; i -= 1)
            {
                var d = ulongData[i];

                var hv = Bucket(d);

                Write(collideP, i, Read(landmarkP, hv));

                Write(landmarkP, hv, i + 1);

            }
        }

        _collide = collide;
        _landmark = landmark;

    }

    public int Match(ReadOnlySpan<byte> data)
    {
        //we assume data is at least 8 bytes long to do a branchless read
        var ulongData = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(data));

        var bucket = Bucket(ulongData);

        var pos = Read(_landmark, bucket);

        return (int)((uint)pos - 1) * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos)
    {
        var a = (uint)pos / 8;
        var b = Read(_collide, (int)a);

        return (b - 1) * 8;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Bucket(ulong x)
    {
        return (int)(Hash.Hash64(x) & _nHashBitMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void Write(byte* p, int offset, int value)
    {
        var low = (byte*)(p + (uint)offset * 3);
        *low = (byte)value;

        var high = (ushort*)(low + 1);
        *high = (ushort)(value >> 8);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int Read(ReadOnlySpan<byte> arr, int offset)
    {
        var v32p = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(arr), offset * 3));
        return (int)((v32p) & 0x00_ff_ff_ff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int Read(byte* p, int offset)
    {
        //read a whole uint and mask the last byte

        var v32p = (uint*)(p + (uint)offset * 3);
        return (int)((*v32p) & 0x00_ff_ff_ff);
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
        var length = _landmark.Length / 3;
        var landmark = length;
        var collide = length;

        for (var i = 0; i < length; i++)
        {
            if (Read(_landmark, i) == 0) landmark--;
            if (Read(_collide, i) == 0) collide--;
        }
        return new HashtableStats
        {
            Collisions = collide,
            Slots = length,
            SlotsFilled = landmark
        };
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
#endif
