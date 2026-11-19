namespace DeltaSharp.Format;

/// <summary>Decodes command headers from a delta format.</summary>
public interface IDeltaReader
{
    /// <summary>Attempts to decode the next command header at the start of the input.</summary>
    /// <param name="input">The remaining encoded patch, starting at a command boundary.</param>
    /// <param name="command">The decoded command when the method returns <see langword="true"/>.</param>
    /// <param name="consumed">
    /// The number of bytes occupied by the header, including any leading separators, on success.
    /// Insert payload bytes are excluded. Ignore this value on failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a command header was decoded; otherwise <see langword="false"/>
    /// if the input is empty, incomplete, or rejected by the format reader.
    /// </returns>
    /// <remarks>
    /// A successful read does not validate the entire patch, source ranges, checksum, or availability
    /// of insert payload bytes. For an insert, consume the header followed by <see cref="DeltaCommand.Length"/>
    /// payload bytes before reading the next command. Implementations may throw for malformed input.
    /// </remarks>
    /// <exception cref="DeltaReaderException">The reader detects invalid command syntax or an unsupported numeric value.</exception>
    bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed);

}
