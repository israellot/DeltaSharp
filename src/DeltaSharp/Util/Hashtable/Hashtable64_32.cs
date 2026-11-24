using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

/// <summary>
/// Stores source landmarks and collision chains using 32-bit entries in a pooled array.
/// The low 28 bits hold a one-based source block index and the high four bits hold
/// the low nibble of its first byte. Zero marks an empty entry.
/// </summary>
internal unsafe readonly ref struct Hashtable64_32
#if NET9_0_OR_GREATER
    : IHashtable
#endif
{

    // An int-sized source contains at most (1 << 28) - 1 complete eight-byte blocks.
    private const int TagShift = 28;
    private const int BlockIndexMask = (1 << TagShift) - 1;

    private readonly ReadOnlySpan<int> _collide;

    private readonly ReadOnlySpan<int> _landmark;

    private readonly ulong _nHashBitMask;

    private readonly int[] _buffer;

    public Hashtable64_32(ReadOnlySpan<byte> data)
    {
        var nLong = (int)data.Length / 8;
                
        // Compute the hash table used to locate matching sections in the source.
        int nHash = Hash.TableSize(nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        _buffer = ArrayPool<int>.Shared.Rent(nHash * 2);        
        _buffer.AsSpan(0, nHash * 2).Clear();

        Span<int> landmark = _buffer.AsSpan().Slice(nHash, nHash);
        Span<int> collide = _buffer.AsSpan().Slice(0, nHash);
        

        ReadOnlySpan<ulong> ulongData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nLong);

        unsafe
        {
            fixed (int* collideP = collide)
            fixed (int* landmarkP = landmark)
            {
                for (var i = nLong - 1; i >= 0; i -= 1)
                {
                    var d = ulongData[i];

                    var hv = Bucket(d);

                    *(collideP + i) = landmarkP[hv];
                    *(landmarkP + hv) = FirstByteTag(d) | (i + 1);
                }
            }
        }

        _collide = collide;
        _landmark = landmark;

    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Match(ReadOnlySpan<byte> data) => FirstCandidate(data, out _, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos) => Position(NextEntry(pos));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int NextEntry(int pos) => _collide[(int)((uint)pos / 8)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Position(int entry) => ((entry & BlockIndexMask) - 1) * 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstCandidate(ReadOnlySpan<byte> data, out int tag, out bool mayMatch)
    {
        var key = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(data));
        tag = FirstByteTag(key);
        return DecodeCandidate(_landmark[Bucket(key)], tag, out mayMatch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextCandidate(int pos, int tag, out bool mayMatch)
        => DecodeCandidate(NextEntry(pos), tag, out mayMatch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstByteTag(ulong key)
        => (int)((BitConverter.IsLittleEndian ? key : key >> 56) & 0xf) << TagShift;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecodeCandidate(int entry, int tag, out bool mayMatch)
    {
        mayMatch = (entry & ~BlockIndexMask) == tag;
        return Position(entry);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Bucket(ulong x)
    {
        return (int)(Hash.Hash64(x) & _nHashBitMask);
    }

    public HashtableStats GetStats()
    {
        return new HashtableStats
        {
            Collisions = _collide.Length - _collide.Count(0),
            Slots = _landmark.Length,
            SlotsFilled = _landmark.Length - _landmark.Count(0)
        };
    }

    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_buffer);
    }
}
