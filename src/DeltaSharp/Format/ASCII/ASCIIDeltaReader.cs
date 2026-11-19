using System.Buffers.Text;

namespace DeltaSharp.Format;

/// <summary>Reads DeltaSharp ASCII command headers with decimal integer operands.</summary>
/// <remarks>Insert payloads contain raw bytes and need not be ASCII text.</remarks>
public class ASCIIDeltaReader : IDeltaReader
{
    /// <inheritdoc cref="IDeltaReader.TryRead"/>
    public bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed)
    {
        command = default;
        consumed = 0;
        var originalLength = input.Length;

        while (!input.IsEmpty && (input[0] == '\n' || input[0] == '\r'))
            input = input.Slice(1);

        if (input.IsEmpty)
            return false;

        var commandByte = input[0];
        if (commandByte != 'l' && commandByte != 'c' && commandByte != 'i' && commandByte != 'v')
            throw new DeltaReaderException("Invalid command");

        if (input.Length < 2)
            return false;
        if (input[1] != ' ')
            throw new DeltaReaderException("Expected whitespace");
        input = input.Slice(2);

        if (!Utf8Parser.TryParse(input, out ulong first, out var firstBytes))
            throw new DeltaReaderException("Failed to parse number");
        input = input.Slice(firstBytes);

        ulong position = 0;
        ulong length = first;
        if (commandByte == 'c')
        {
            if (input.IsEmpty || input[0] != ' ')
                throw new DeltaReaderException("Expected whitespace");
            input = input.Slice(1);
            position = first;
            if (!Utf8Parser.TryParse(input, out length, out var lengthBytes))
                throw new DeltaReaderException("Failed to parse number");
            input = input.Slice(lengthBytes);
        }

        if (commandByte == 'v')
        {
            if (!input.IsEmpty && input[0] != '\n' && input[0] != '\r')
                throw new DeltaReaderException("Expected end of checksum");
        }
        else
        {
            if (input.IsEmpty)
                return false;

            // Insert data can follow a space (writer format) or a line ending.
            // Consume only the separator: literal data may itself start with CR/LF.
            if (commandByte == 'i' && input[0] == ' ')
                input = input.Slice(1);
            else if (input[0] == '\n')
                input = input.Slice(1);
            else if (input[0] == '\r')
            {
                if (input.Length < 2)
                    return false;
                if (input[1] != '\n')
                    throw new DeltaReaderException("Expected newline");
                input = input.Slice(2);
            }
            else
                throw new DeltaReaderException("Expected newline");
        }

        var kind = commandByte switch
        {
            (byte)'l' => DeltaCommandEnum.Length,
            (byte)'c' => DeltaCommandEnum.Copy,
            (byte)'i' => DeltaCommandEnum.Insert,
            _ => DeltaCommandEnum.Checksum
        };
        command = new DeltaCommand(kind, position, length);
        consumed = (uint)(originalLength - input.Length);
        return true;
    }
}
