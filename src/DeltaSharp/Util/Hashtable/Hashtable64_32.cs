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
/// Stores source landmarks and collision chains using 32-bit, zero-based block indices
/// in a pooled integer array, with -1 marking empty slots. The indices address all complete
/// 8-byte blocks in an int-sized source span; allocation remains subject to available memory
/// and runtime array-size limits.
/// </summary>
internal unsafe readonly ref struct Hashtable64_32
#if NET9_0_OR_GREATER
    : IHashtable
#endif
{

    private readonly ReadOnlySpan<int> _collide;

    private readonly ReadOnlySpan<int> _landmark;

    private readonly ulong _nHashBitMask;

    private readonly int[] _buffer;

    public Hashtable64_32(ReadOnlySpan<byte> data)
    {
        //In C# arrays are limited to Int32.MaxValue elements
        //So we can safely assume here that the data length will not exceed 2,147,483,640 bytes
        //We can use the sign bit to indicate empty slots and avoid the extra checks and shifts needed in the other versions

        var nLong = (int)data.Length / 8;
                
        // Compute the hash table used to locate matching sections in the source.
        int nHash = Hash.TableSize(nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        _buffer = ArrayPool<int>.Shared.Rent(nHash * 2);        
        _buffer.AsSpan().Slice(0, nHash * 2).Fill(-1); //use sign bit to indicate empty

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
                    *(landmarkP + hv) = i;
                }
            }
        }

        _collide = collide;
        _landmark = landmark;

    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Match(ReadOnlySpan<byte> data)
    {
        //assume data is at least 8 bytes long to do a branchless read
        var ulongData = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(data));

        var bucket = Bucket(ulongData);

        //branchless read since bucket will always be within range
        int pos = _landmark[bucket];

        return pos * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos)
    {
        //branchless read
        //index is always within range
        return _collide[(int)((uint)pos / 8)] * 8;
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
            Collisions = _collide.Length - _collide.Count(-1),
            Slots = _landmark.Length,
            SlotsFilled = _landmark.Length - _landmark.Count(-1)
        };
    }

    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_buffer);
    }
}
