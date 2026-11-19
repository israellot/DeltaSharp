namespace DeltaSharp.Format;

/// <summary>Describes a decoded delta command without retaining its encoded bytes or insert payload.</summary>
public readonly ref struct DeltaCommand
{
    /// <summary>Gets the operation represented by this command.</summary>
    public readonly DeltaCommandEnum Command { get; }

    /// <summary>Gets the zero-based source byte offset for a copy command; unused for other commands.</summary>
    public readonly ulong Position { get; }

    /// <summary>Gets the output length, insert or copy byte count, or checksum value, according to <see cref="Command"/>.</summary>
    public readonly ulong Length { get; }

    /// <summary>Initializes a command with the supplied operands without validating them.</summary>
    /// <param name="command">The operation to represent.</param>
    /// <param name="position">The source byte offset for a copy command; normally zero for other commands.</param>
    /// <param name="length">The target length, insert or copy byte count, or checksum value for the operation.</param>
    public DeltaCommand(DeltaCommandEnum command, ulong position, ulong length)
    {
        Command = command;
        Position = position;
        Length = length;
    }

}

/// <summary>Identifies the operations in a delta command stream.</summary>
public enum DeltaCommandEnum
{
    /// <summary>Declares the total reconstructed target length in bytes.</summary>
    Length,
    /// <summary>Appends literal bytes following the command header to the target.</summary>
    Insert,
    /// <summary>Appends a range of bytes from the original source to the target.</summary>
    Copy,
    /// <summary>Supplies the target checksum and terminates the patch.</summary>
    Checksum
}
