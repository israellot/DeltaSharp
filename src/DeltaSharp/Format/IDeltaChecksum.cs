using System.Buffers.Binary;

namespace DeltaSharp.Format;

/// <summary>Computes the target checksum stored in and verified against a patch.</summary>
/// <remarks>Patch creation and application must use the same checksum algorithm.</remarks>
public interface IDeltaChecksum
{
    /// <summary>Computes a checksum over the complete input.</summary>
    /// <param name="data">The target bytes to checksum; may be empty.</param>
    /// <returns>The 32-bit checksum produced by this implementation.</returns>
    uint Checksum(ReadOnlySpan<byte> data); 

}
