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
/// Specialized hashtable to handle data of 524,280 bytes or less in size
/// The trick here is avoiding heap allocations and using a stack allocated fixed size buffer
/// </summary>
internal unsafe readonly ref struct Hashtable64_16
{

    private readonly ReadOnlySpan<ushort> _collide;

    private readonly ReadOnlySpan<ushort> _landmark;

    private readonly ushort* _collideP;
    private readonly ushort* _landmarkP;

    private readonly ulong _nHashBitMask;

    private readonly ushort[] _buffer;

    public Hashtable64_16(ReadOnlySpan<byte> data)
    {
        var nLong = (int)data.Length / 8;

        if (nLong > ushort.MaxValue)
            throw new ArgumentException($"Max allowed size is {ushort.MaxValue * 8}", nameof(data));

        // Compute the hash table used to locate matching sections in the source.        
        int nHash = (int)BitOperations.RoundUpToPowerOf2((uint)nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        ushort[] buffer;

        if (nHash > 10_000) //avoid LOH
        {
            _buffer = ArrayPool<ushort>.Shared.Rent(nHash * 2);
            _buffer.AsSpan().Slice(0, nHash * 2).Fill(0);
            buffer = _buffer;
        }
        else
        {
            buffer = new ushort[nHash * 2];
        }

        Span<ushort> collide = buffer.AsSpan().Slice(0, nHash);
        Span<ushort> landmark = buffer.AsSpan().Slice(nHash, nHash);

        _landmarkP = (ushort*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(landmark));
        _collideP = (ushort*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(collide));

        ReadOnlySpan<ulong> ulongData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nLong);

        for (var i = nLong - 1; i >= 0; i -= 1)
        {
            var d = ulongData[i];

            var hv = Bucket(d);

            _collideP[i] = _landmarkP[hv];

            _landmarkP[hv] = (ushort)(i + 1);
        }

        _collide = collide;
        _landmark = landmark;

    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Match(ReadOnlySpan<byte> data)
    {
        //we assume data is at least 8 bytes long to do a branchless read
        var ulongData = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(data));

        var bucket = Bucket(ulongData);

        var pos = *(_landmarkP + (uint)bucket);

        return (pos - 1) * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos)
    {
        var index = (uint)pos / 8;
        var b = *(_collideP + index);

        return (b - 1) * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Bucket(ulong x)
    {
        return (int)(Hash.Hash64(x) & _nHashBitMask);
    }

    public void Dispose()
    {
        if (_buffer is not null)
            ArrayPool<ushort>.Shared.Return(_buffer);
    }

    public HashtableStats GetStats()
    {
        return new HashtableStats
        {
            Collisions = _collide.Length - _collide.Count((byte)0),
            Slots = _landmark.Length,
            SlotsFilled = _landmark.Length - _landmark.Count((byte)0)
        };
    }
}