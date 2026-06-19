using DeltaSharp.Util;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;

namespace DeltaSharp.Format;

public class FossilDeltaReader : IDeltaReader
{

    static readonly int[] _zValue = {
            -1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
            0,  1,  2,  3,  4,  5,  6,  7,    8,  9, -1, -1, -1, -1, -1, -1,
            -1, 10, 11, 12, 13, 14, 15, 16,   17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32,   33, 34, 35, -1, -1, -1, -1, 36,
            -1, 37, 38, 39, 40, 41, 42, 43,   44, 45, 46, 47, 48, 49, 50, 51,
            52, 53, 54, 55, 56, 57, 58, 59,   60, 61, 62, -1, -1, -1, 63, -1
        };

    public FossilDeltaReader()
    {

    }

    private unsafe (uint value, uint consumed) ReadInt(ReadOnlySpan<byte> input)
    {
        uint consumed = 0;
        fixed (int* zValue = _zValue)
        {
            uint v = 0;
            int c;
            while (!input.IsEmpty && (c = zValue[0x7f & input[0]]) >= 0)
            {
                // Each digit shifts the accumulator left by 6 bits. If the top 6 bits
                // are already set, another digit would silently overflow the 32-bit
                // value and corrupt the parsed length/offset. Reject instead.
                if ((v & 0xFC00_0000u) != 0)
                    throw new DeltaReaderException("integer too large in delta");

                v = (v << 6) + (uint)c;
                consumed++;
                input = input.Slice(1);
            }
            return (v, consumed);
        }


    }

    public bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed)
    {
        command = default;
        consumed = 0;

        if (input.IsEmpty)
            return false;

        (var cnt, var read) = ReadInt(input);
        consumed += read;
        input = input.Slice((int)read);

        // ReadInt may have consumed every remaining byte (e.g. a delta that is all
        // base64 digits and carries no command byte). Guard before dereferencing,
        // otherwise input[0] throws IndexOutOfRangeException instead of cleanly
        // reporting an invalid/incomplete delta via the TryRead contract.
        if (input.IsEmpty)
            return false;

        var cmd = input[0];
        consumed++;

        input = input.Slice(1);

        if (cmd == (byte)'\n')
        {
            command = new DeltaCommand(DeltaCommandEnum.Length, 0, cnt);

            return true;
        }
        if (cmd == (byte)'@')
        {
            (var offset, read) = ReadInt(input);
            consumed += read;

            input = input.Slice((int)read);
            if (input.IsEmpty || input[0] != ',')
                throw new DeltaReaderException("copy command not terminated by ','");

            input = input.Slice(1);
            consumed++;//account for ','

            if (cnt == 0)
                throw new DeltaReaderException("invalid zero length copy command");

            command = new DeltaCommand(DeltaCommandEnum.Copy, offset, cnt);

            return true;

        }
        else if (cmd == (byte)':')
        {
            if (cnt == 0)
                throw new DeltaReaderException("invalid zero length insert command");

            command = new DeltaCommand(DeltaCommandEnum.Insert, 0, cnt);

            return true;
        }
        else if (cmd == (byte)';')
        {
            command = new DeltaCommand(DeltaCommandEnum.Checksum, 0, cnt);

            return true;
        }

        return false;
    }


}

