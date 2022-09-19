using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeltaSharp;



internal readonly ref struct Hashtable64
{
    readonly Hashtable64_8 _hashtable8;
    readonly Hashtable64_16 _hashtable16;
    readonly Hashtable64_24 _hashtable24;
    readonly Hashtable64_32 _hashtable32;
    readonly byte _hashtable;     

    public Hashtable64(ReadOnlySpan<byte> data)
    {
        if(data.Length < byte.MaxValue * 4)
        {
            _hashtable8 = new Hashtable64_8(data);
            _hashtable = 8;
        }
        else if (data.Length < ushort.MaxValue * 8)
        {
            _hashtable16 = new Hashtable64_16(data);
            _hashtable = 16;
        }
        else if(data.Length < 0xff_ff_ff * 8)
        {
            _hashtable24 = new Hashtable64_24(data);
            _hashtable = 24;
        }
        else
        {
            _hashtable32 = new Hashtable64_32(data);
            _hashtable = 32;
        }
    }
        
    public int Match(ReadOnlySpan<byte> data)
    {
        switch (_hashtable)
        {
            case 8: return _hashtable8.Match(data);
            case 16: return _hashtable16.Match(data);
            case 24: return _hashtable24.Match(data);
            default: return _hashtable32.Match(data);
        }
    }
       
    public int More(int pos)
    {
        switch (_hashtable)
        {
            case 8: return _hashtable8.More(pos);
            case 16: return _hashtable16.More(pos);
            case 24: return _hashtable24.More(pos);
            default: return _hashtable32.More(pos);
        }
    }

    public int BlockSize()
    {
        return 8;
        //switch (_hashtable)
        //{                      
        //    default: return 8;
        //}
    }

    public void Dispose()
    {
        switch (_hashtable)
        {
            case 8: _hashtable8.Dispose();break;
            case 16: _hashtable16.Dispose(); break;
            case 24: _hashtable24.Dispose(); break;
            default: _hashtable32.Dispose(); break;
        }
    }
}

internal readonly ref struct HashtableStats
{
    public readonly int Collisions { get; init; }
    public readonly int Slots { get; init; }
    public readonly int SlotsFilled { get; init; }
}

internal readonly ref struct Hashtable64_32
{

    private readonly ReadOnlySpan<int> _collide;

    private readonly ReadOnlySpan<int> _landmark;

    private readonly ulong _nHashBitMask;

    private readonly int[] _buffer;

    public Hashtable64_32(ReadOnlySpan<byte> data)
    {
        var nLong = (int)data.Length / 8;
                
        // Compute the hash table used to locate matching sections in the source.
        int nHash = (int)BitOperations.RoundUpToPowerOf2((uint)nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        _buffer= ArrayPool<int>.Shared.Rent(nHash * 2);
        _buffer.AsSpan().Slice(0,nHash*2).Fill(-1);

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
        var ulongData = MemoryMarshal.Read<ulong>(data);

        var bucket = Bucket(ulongData);

        var pos = _landmark[bucket];

        return pos * 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int More(int pos)
    {        
        return _collide[(int)((uint)pos/8)] * 8;
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

internal readonly ref struct Hashtable64_24
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
        int nHash = (int)BitOperations.RoundUpToPowerOf2((uint)nLong);

        _nHashBitMask = (ulong)(nHash - 1);

        var arraySize = (nHash * 3 + 1);

        _buffer=ArrayPool<byte>.Shared.Rent(arraySize * 2);
        _buffer.AsSpan().Slice(0, arraySize * 2).Clear();

        Span<byte> collide = _buffer.AsSpan(0, arraySize);
        Span<byte> landmark = _buffer.AsSpan(arraySize, arraySize);

        ReadOnlySpan<ulong> ulongData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nLong);

        unsafe
        {
            fixed(byte* collideP = collide)
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
        }

        _collide = collide;
        _landmark = landmark;

    }

    public int Match(ReadOnlySpan<byte> data)
    {
        var ulongData = MemoryMarshal.Read<ulong>(data);

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
        //B1 B2 B3 B4
        //N1 N2 N3 _
        //0  N1 N2 N3

        var v32p = (uint*)(p + (uint)offset * 3);
        var v32 = *v32p;
        v32 = (v32 & 0xff_00_00_00) | ((uint)value);

        *v32p = v32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void WriteUnsafe(byte* p, int offset, int value)
    {
        //B1 B2 B3 B4
        //N1 N2 N3 N4
        //0  N1 N2 N3

        var v32p = (uint*)(p + (uint)offset * 3);
        *v32p = (uint)value;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int Read(ReadOnlySpan<byte> arr, int offset)
    {
        //B1 B2 B3 B4
        //N1 N2 N3 N4
        //0  N1 N2 N3

        fixed (byte* p = arr)
        {
            var v32p = (uint*)(p + (uint)offset * 3);

            return (int)((*v32p) & 0x00_ff_ff_ff);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe int Read(byte* p, int offset)
    {
        var v32p = (uint*)(p + (uint)offset * 3);
        return (int)((*v32p) & 0x00_ff_ff_ff);
    }

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

internal readonly ref struct Hashtable64_16
{

    private readonly ReadOnlySpan<ushort> _collide;

    private readonly ReadOnlySpan<ushort> _landmark;

    private readonly ulong _nHashBitMask;

    private readonly ushort[] _buffer;

    public Hashtable64_16(ReadOnlySpan<byte> data)
    {
        var nLong = (int)data.Length / 8;

        if (nLong > ushort.MaxValue)
            throw new ArgumentException($"Max allowed size is {ushort.MaxValue*8}", nameof(data));

        // Compute the hash table used to locate matching sections in the source.        
        int nHash = (int)BitOperations.RoundUpToPowerOf2((uint)nLong);
        
        _nHashBitMask = (ulong)(nHash - 1);

        ushort[] buffer;

        if(nHash > 10_000) //avoid LOH
        {
            _buffer = ArrayPool<ushort>.Shared.Rent(nHash * 2);
            _buffer.AsSpan().Slice(0,nHash * 2).Clear();
            buffer = _buffer;
        }
        else
        {
            buffer=new ushort[nHash * 2];
        }

        Span<ushort> collide = buffer.AsSpan().Slice(0, nHash);
        Span<ushort> landmark = buffer.AsSpan().Slice(nHash, nHash);

        //Span<ushort> collide = _memoryOwner.Memory.Span.Slice(0, nHash);
        //Span<ushort> landmark = _memoryOwner.Memory.Span.Slice(nHash, nHash);

        //Span<ushort> collide = new ushort[nHash * 2];
        //Span<ushort> landmark = collide.Slice(nHash);
        //collide = collide.Slice(0, nHash);

        ReadOnlySpan<ulong> ulongData = MemoryMarshal.Cast<byte, ulong>(data).Slice(0, nLong);

        for (var i = nLong - 1; i >= 0; i -= 1)
        {
            var d = ulongData[i];

            var hv = Bucket(d);

            collide[i] = landmark[hv];

            landmark[hv] = (ushort)(i + 1);
        }

        _collide = collide;
        _landmark = landmark;

    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int Match(ReadOnlySpan<byte> data)
    {
        var ulongData = MemoryMarshal.Read<ulong>(data);

        var bucket = Bucket(ulongData);

        fixed (ushort* p = _landmark)
        {
            var pos = *(p + (uint)bucket);

            return (pos - 1) * 8;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int More(int pos)
    {
        fixed(ushort* p = _collide)
        {
            var a = (uint)pos / 8;
            var b = *(p + a);

            return (b - 1) * 8;
        }
    }            

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Bucket(ulong x)
    {
        return (int)(Hash.Hash64(x) & _nHashBitMask);
    }

    public void Dispose()
    {
        if(_buffer is not null)
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

internal readonly ref struct Hashtable64_8
{
    [InlineArray(256)]
    struct ByteArray256
    {
        byte Element;
    }

    private readonly ByteArray256 _collide;
    private readonly ByteArray256 _landmark;

    private const ulong _nHashBitMask = 255;


    public Hashtable64_8(ReadOnlySpan<byte> data)
    {
        var nInt = (int)data.Length / 8;

        if (nInt > byte.MaxValue)
            throw new ArgumentException($"Max allowed size is {byte.MaxValue*8}", nameof(data));

        for(var i=0;i<256; i++)
        {
            _collide[i] = 0;
            _landmark[i] = 0;
        }

        //// Compute the hash table used to locate matching sections in the source.        
        //int nHash = 256;

        //_nHashBitMask = (ulong)(nHash - 1);

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
        var uLongData = MemoryMarshal.Read<ulong>(data);

        var bucket = Bucket(uLongData);

        var pos =_landmark[bucket];

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

    public HashtableStats GetStats()
    {
        var collisions = 256;
        var filled = 256;
        for(var i = 0; i < 256; i++)
        {
            if (_collide[i]==0) collisions--;
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

internal readonly ref struct Hash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash64(ulong x)
    {
        //  SplitableRandom 
        // https://nullprogram.com/blog/2018/07/31/
        unchecked
        {
            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9U;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebU;
            x ^= x >> 31;
            return x;
        }
        
    }
        

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash32(uint x)
    {
        //https://nullprogram.com/blog/2018/07/31/

        unchecked
        {
            x ^= x >> 17;
            x *= 0xed5ad4bbU;
            x ^= x >> 11;
            x *= 0xac4c1b51U;
            x ^= x >> 15;
            x *= 0x31848babU;
            x ^= x >> 14;
        }
        

        return x;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash32(ReadOnlySpan<byte> bytes)
    {
        uint seed = 0;
        ref byte bp = ref MemoryMarshal.GetReference(bytes);
        ref uint endPoint = ref Unsafe.Add(ref Unsafe.As<byte, uint>(ref bp), bytes.Length >> 2);

        do
        {
            var a = Unsafe.ReadUnaligned<uint>(ref bp) * 3432918353U;
            var b = BitOperations.RotateLeft(a, 15);
            var c = seed ^ b * 461845907U;
            var d = BitOperations.RotateLeft(c, 13);
            seed = (d * 5) - 430675100;

            bp = ref Unsafe.Add(ref bp, 4);
        } while (Unsafe.IsAddressLessThan(ref Unsafe.As<byte, uint>(ref bp), ref endPoint));

        var remainder = bytes.Length & 3;

        uint num = 0;
        switch (remainder)
        {
            case 3:
                {
                    num ^= endPoint;
                    num ^= Unsafe.Add(ref endPoint, 1) << 8;
                    num ^= Unsafe.Add(ref endPoint, 2) << 16;
                    seed ^= BitOperations.RotateLeft(num * 3432918353U, 15) * 461845907U;
                    break;
                }
            case 2:
                {
                    num ^= endPoint;
                    num ^= Unsafe.Add(ref endPoint, 1) << 8;
                    seed ^= BitOperations.RotateLeft(num * 3432918353U, 15) * 461845907U;
                    break;
                }
            case 1:
                {
                    num ^= endPoint;
                    seed ^= BitOperations.RotateLeft(num * 3432918353U, 15) * 461845907U;
                    break;
                }
        }

        return Hash32((uint)seed);
    }
}

