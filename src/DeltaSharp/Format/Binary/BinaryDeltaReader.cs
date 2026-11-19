using DeltaSharp.Util;

namespace DeltaSharp.Format;

/// <summary>Reads DeltaSharp binary command headers with variable-length integer operands.</summary>
public class BinaryDeltaReader : IDeltaReader
{

    /// <inheritdoc cref="IDeltaReader.TryRead"/>
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

/// <summary>Reports invalid syntax or an unsupported numeric value in an encoded delta command.</summary>
public class DeltaReaderException : Exception
{
    /// <summary>Initializes a reader exception with the default exception message.</summary>
    public DeltaReaderException() { }

    /// <summary>Initializes a reader exception with a description of the parsing failure.</summary>
    /// <param name="message">The description of the invalid command.</param>
    public DeltaReaderException(string message) : base(message) { }
}
