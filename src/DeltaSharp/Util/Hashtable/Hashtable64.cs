using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

/// <summary>
/// Specialized hashtable to store landmarks and respective collisions
/// Because we hash at every 8 bytes, we can store the address as multiple of 8
/// To improve memory usage, there are specialized implementations according to data size
/// For example, for a data size of 2048 bytes or less, we can use use a single byte to store the address and hash.
/// </summary>
internal readonly ref struct Hashtable64
{
    public readonly int BlockSize { get; } = 8;

    private readonly Hashtable64_8 _hashtable8;
    private readonly Hashtable64_16 _hashtable16;
    private readonly Hashtable64_24 _hashtable24;
    private readonly Hashtable64_32 _hashtable32;
    private readonly byte _hashtable;

    public Hashtable64(ReadOnlySpan<byte> data)
    {
        if (data.Length < byte.MaxValue * 4)
        {
            _hashtable8 = new Hashtable64_8(data);
            _hashtable = 8;
        }
        else if (data.Length < ushort.MaxValue * 8)
        {
            _hashtable16 = new Hashtable64_16(data);
            _hashtable = 16;
        }
        else if (data.Length < 0xff_ff_ff * 8)
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

    

    public void Dispose()
    {
        switch (_hashtable)
        {
            case 8: _hashtable8.Dispose(); break;
            case 16: _hashtable16.Dispose(); break;
            case 24: _hashtable24.Dispose(); break;
            default: _hashtable32.Dispose(); break;
        }
    }
}
