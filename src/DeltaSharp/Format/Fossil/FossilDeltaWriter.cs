using DeltaSharp.Util;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;

namespace DeltaSharp.Format;
internal class FossilDeltaWriter : IDeltaWriter
{

    private readonly static uint[] _zDigits = {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D',
            'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '_', 'a', 'b', 'c', 'd', 'e',
            'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
            't', 'u', 'v', 'w', 'x', 'y', 'z', '~'
        };

    private MemoryStream _ms;
    private byte[] _buffer => _ms.GetBuffer();
    private int _position => (int)_ms.Position;

    public FossilDeltaWriter()
    {
        _ms = new MemoryStream();
    }

    public FossilDeltaWriter(int sizeHint)
    {
        _ms = new MemoryStream(sizeHint);
    }

    private unsafe void WriteInt(ulong v)
    {
        int i, j;

        if (v == 0)
        {
            _ms.WriteByte((byte)'0');
            return;
        }

        fixed (uint* zBuf = stackalloc uint[20])
        fixed (uint* zDigits = _zDigits)
        {
            for (i = 0; v > 0; i++, v >>= 6)
            {
                zBuf[i] = zDigits[v & 0x3f];
            }
            for (j = i - 1; j >= 0; j--)
            {
                var s = (byte)zBuf[j];
                _ms.WriteByte(s);
            }
        }
    }


    public void WriteLength(ulong length)
    {
        //write length
        WriteInt(length);

        //write line ending
        _ms.Write("\n"u8);
    }

    public void WriteCopy(ulong position, ulong length)
    {
        //write length
        WriteInt(length);

        //write command
        _ms.WriteByte((byte)'@');

        //write copy position
        WriteInt(position);

        //write copy end
        _ms.WriteByte((byte)',');
    }

    public void WriteInsert(ReadOnlySpan<byte> s)
    {
        //write length
        WriteInt((ulong)s.Length);

        //write command
        _ms.WriteByte((byte)':');

        //write data
        _ms.Write(s);
    }

    public void WriteChecksum(uint checksum)
    {
        //write length
        WriteInt((ulong)checksum);

        //write command
        _ms.WriteByte((byte)';');

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
