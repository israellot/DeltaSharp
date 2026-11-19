namespace DeltaSharp.Format;

/// <summary>Encodes delta commands and retains the resulting patch bytes.</summary>
/// <remarks>
/// Write the target length first, then copy and insert commands, and finally the target checksum.
/// Implementations used with <see cref="DeltaCreate{TWriter, TChecksum}"/> must keep returned output
/// memory valid after disposal because the creator disposes its writer before returning the result.
/// </remarks>
public interface IDeltaWriter:IDisposable
{
    /// <summary>Writes the header declaring the reconstructed target length.</summary>
    /// <param name="length">The target length in bytes.</param>
    void WriteLength(ulong length);

    /// <summary>Writes an insert command and its literal payload.</summary>
    /// <param name="s">The bytes to append to the reconstructed target.</param>
    void WriteInsert(ReadOnlySpan<byte> s);

    /// <summary>Writes a command that copies bytes from the original source.</summary>
    /// <param name="position">The zero-based byte offset in the source.</param>
    /// <param name="length">The number of source bytes to copy.</param>
    void WriteCopy(ulong position, ulong length);

    /// <summary>Writes the checksum command that terminates the patch.</summary>
    /// <param name="checksum">The checksum of the complete reconstructed target.</param>
    void WriteChecksum(uint checksum);

    /// <summary>Gets the encoded bytes written so far.</summary>
    /// <returns>The patch bytes, which may reference the writer's backing storage without a copy.</returns>
    /// <remarks>Complete all writes before retrieving the final output; this method does not finalize or validate the patch.</remarks>
    ReadOnlyMemory<byte> GetOutput();
}
