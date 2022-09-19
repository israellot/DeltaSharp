using DeltaSharp.Util;
using System.Buffers.Text;
using System.Text;

namespace DeltaSharp.Format;
internal class ASCIIDeltaWriter : IDeltaWriter
{
    private MemoryStream _ms;
    private byte[] _buffer => _ms.GetBuffer();
    private int _position => (int)_ms.Position;

    public ASCIIDeltaWriter()
    {
        _ms = new MemoryStream();
    }

    public ASCIIDeltaWriter(int sizeHint)
    {
        _ms = new MemoryStream(sizeHint);
    }

    public void WriteLength(ulong length)
    {
        //write command
        _ms.Write("l "u8);

        Span<byte> encoded = stackalloc byte[24];
        Utf8Formatter.TryFormat(length, encoded, out var written);
        _ms.Write(encoded.Slice(0, written));

        //write line ending
        _ms.Write("\n"u8);
    }

    public void WriteCopy(ulong position, ulong length)
    {
        //write command
        _ms.Write("c "u8);

        Span<byte> encoded = stackalloc byte[24];

        //write position
        Utf8Formatter.TryFormat(position, encoded, out var written);
        _ms.Write(encoded.Slice(0, written));

        //write separator
        _ms.Write(" "u8);

        //write length
        Utf8Formatter.TryFormat(length, encoded, out written);
        _ms.Write(encoded.Slice(0, written));

        //write line ending
        _ms.Write("\n"u8);
    }

    public void WriteInsert(ReadOnlySpan<byte> s)
    {
        //write command
        _ms.Write("i "u8);

        //write insert length
        Span<byte> encoded = stackalloc byte[24];
        Utf8Formatter.TryFormat(s.Length, encoded, out var written);
        _ms.Write(encoded.Slice(0, written));

        //write separator
        _ms.Write(" "u8);

        //write insert data
        _ms.Write(s);

        //write line ending
        _ms.Write("\n"u8);
    }

    public void WriteChecksum(uint checksum)
    {
        //write command
        _ms.Write("v "u8);

        //write checksum value
        Span<byte> encoded = stackalloc byte[24];
        Utf8Formatter.TryFormat(checksum, encoded, out var written);
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
