using DeltaSharp.Util;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeltaSharp.Format;
internal class BinaryDeltaWriter : IDeltaWriter
{
    private MemoryStream _ms;
    private byte[] _buffer => _ms.GetBuffer();
    private int _position => (int)_ms.Position;

    public BinaryDeltaWriter()
    {
        _ms = new MemoryStream();
    }

    public BinaryDeltaWriter(int sizeHint)
    {
        _ms = new MemoryStream(sizeHint);
    }

    public void WriteLength(ulong length)
    {
        _ms.WriteByte((byte)'l');

        Span<byte> encoded = stackalloc byte[9];
        var written=VarIntFormatter.EncodeUnsafe(length, encoded);
        _ms.Write(encoded.Slice(0, written));
    }

    public void WriteCopy(ulong position, ulong length)
    {
        _ms.WriteByte((byte)'c');

        Span<byte> encoded = stackalloc byte[9];

        //write position
        var written=VarIntFormatter.EncodeUnsafe(position, encoded);
        _ms.Write(encoded.Slice(0, written));

        //write length
        written=VarIntFormatter.EncodeUnsafe(length, encoded);
        _ms.Write(encoded.Slice(0, written));
    }

    public void WriteInsert(ReadOnlySpan<byte> s)
    {
        _ms.WriteByte((byte)'i');

        //write insert length
        Span<byte> encoded = stackalloc byte[9];
        var written=VarIntFormatter.EncodeUnsafe((ulong)s.Length, encoded);
        _ms.Write(encoded.Slice(0, written));

        //write insert data
        _ms.Write(s);
    }

    public void WriteChecksum(uint checksum)
    {
        _ms.WriteByte((byte)'v');

        //write checksum value
        Span<byte> encoded = stackalloc byte[9];
        var written=VarIntFormatter.EncodeUnsafe(checksum, encoded);
        _ms.Write(encoded.Slice(0, written));
    }

    public ReadOnlyMemory<byte> GetOutput()
    {
        return _buffer.AsMemory().Slice(0,_position);
    }

    public void Dispose()
    {
        _ms.Dispose();
    }
}
