using DeltaSharp.Util;
using System.Buffers.Text;
using System.Text;

namespace DeltaSharp.Format;

public class ASCIIDeltaReader : IDeltaReader
{
    

    public bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed)
    {
        command = default;
        consumed = 0;

        if (input.IsEmpty)
            return false;

        // Skip leading line breaks. The loop must re-check for end-of-input on every
        // iteration, otherwise input consisting only of '\n'/'\r' slices down to empty
        // and the dereference below throws IndexOutOfRangeException.
        while(!input.IsEmpty && ((char)input[0]=='\n' || (char)input[0]=='\r'))
        {
            consumed++;
            input = input.Slice(1);
        }

        if (input.IsEmpty)
            return false;

        var commandChar = (char)input[0];

        switch (commandChar)
        {
            case 'l':
                {
                    input = input.Slice(1);

                    var newLineIndex = input.IndexOf((byte)'\n');
                    if (newLineIndex < 0)
                        return false;

                    if (input[0] != (byte)' ')
                        throw new DeltaReaderException("Expected whitespace");

                    input = input.Slice(1);

                    if (!Utf8Parser.TryParse(input, out ulong length, out var lengthBytes))
                        throw new DeltaReaderException("Failed to parse number");

                    consumed += (uint)lengthBytes + 3; //number + c + whitespace + newline

                    command = new DeltaCommand(DeltaCommandEnum.Length, 0, length);

                    return true;
                }
            case 'c':
                {
                    input = input.Slice(1);

                    var newLineIndex = input.IndexOf((byte)'\n');
                    if (newLineIndex < 0)
                        return false;

                    if (input[0] != (byte)' ')
                        throw new DeltaReaderException("Expected whitespace");

                    input = input.Slice(1);

                    if (!Utf8Parser.TryParse(input, out ulong position, out var positionBytes))
                        throw new DeltaReaderException("Failed to parse number");

                    // There must actually be a whitespace separator between the two
                    // numbers. Without this check, Slice(positionBytes+1) can run past
                    // the end of a truncated/malformed copy command (no separator).
                    if (positionBytes >= input.Length || input[positionBytes] != (byte)' ')
                        throw new DeltaReaderException("Expected whitespace");

                    input = input.Slice(positionBytes+1);//advance position + whitespace

                    if (!Utf8Parser.TryParse(input, out ulong length, out var lengthBytes))
                        throw new DeltaReaderException("Failed to parse number");

                    consumed += (uint)positionBytes + (uint)lengthBytes + 4; //numbers + c + 2*whitespace + newline

                    command = new DeltaCommand(DeltaCommandEnum.Copy, position, length);

                    return true;
                }
            case 'i':
                {
                    input = input.Slice(1);

                    var newLineIndex = input.IndexOf((byte)'\n');
                    if (newLineIndex < 0)
                        return false;

                    if (input[0] != (byte)' ')
                        throw new DeltaReaderException("Expected whitespace");

                    input = input.Slice(1);

                    if (!Utf8Parser.TryParse(input, out ulong length, out var lengthBytes))
                        throw new DeltaReaderException("Failed to parse number");

                    consumed += (uint)lengthBytes + 3; // number + c + whitespace + newline

                    command = new DeltaCommand(DeltaCommandEnum.Insert, 0, length);

                    return true;
                }
            case 'v':
                {
                    input = input.Slice(1);

                    if(input.IsEmpty)
                        throw new DeltaReaderException("Expected whitespace");

                    if (input[0] != (byte)' ')
                        throw new DeltaReaderException("Expected whitespace");

                    input = input.Slice(1);

                    if (!Utf8Parser.TryParse(input, out ulong length, out var lengthBytes))
                        throw new DeltaReaderException("Failed to parse number");

                    consumed += (uint)lengthBytes + 2; //number + c + whitespace

                    command = new DeltaCommand(DeltaCommandEnum.Checksum, 0, length);

                    return true;
                }
            default:
                throw new DeltaReaderException("Invalid command");
        }
    }

}

