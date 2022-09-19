using DeltaSharp.Util;

namespace DeltaSharp.Format;

public class BinaryDeltaReader : IDeltaReader
{

    public bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed)
    {
        command = default;
        consumed = 0;

        if (input.IsEmpty)
            return false;

        var commandChar = (char)input[0];

        switch (commandChar)
        {
            case 'l':
                {
                    input = input.Slice(1);
                    if (!VarIntFormatter.TryDecode(input, out var length, out var lengthChars))
                        return false;

                    consumed = (uint)lengthChars + 1;

                    command = new DeltaCommand(DeltaCommandEnum.Length, 0, length);

                    return true;
                }
            case 'c':
                {
                    input = input.Slice(1);
                    if (!VarIntFormatter.TryDecode(input, out var position, out var positionBytes))
                        return false;

                    input = input.Slice(positionBytes);
                    if (!VarIntFormatter.TryDecode(input, out var length, out var lengthBytes))
                        return false;

                    consumed = (uint)positionBytes + (uint)lengthBytes + 1;

                    command = new DeltaCommand(DeltaCommandEnum.Copy, position, length);

                    return true;
                }
            case 'i':
                {
                    input = input.Slice(1);
                    if (!VarIntFormatter.TryDecode(input, out var length, out var lengthBytes))
                        return false;

                    consumed = (uint)lengthBytes + 1;

                    command = new DeltaCommand(DeltaCommandEnum.Insert, 0, length);

                    return true;
                }
            case 'v':
                {
                    input = input.Slice(1);
                    if (!VarIntFormatter.TryDecode(input, out var length, out var lengthBytes))
                        return false;

                    consumed = (uint)lengthBytes + 1;

                    command = new DeltaCommand(DeltaCommandEnum.Checksum, 0, length);

                    return true;
                }
            default:
                throw new DeltaReaderException("Invalid command");
        }
    }

}

public class DeltaReaderException : Exception
{
    public DeltaReaderException() { }

    public DeltaReaderException(string message) : base(message) { }
}