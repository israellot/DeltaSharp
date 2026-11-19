using DeltaSharp.Format;
using System;
using static DeltaSharp.DeltaApplyException;

namespace DeltaSharp;

/// <summary>Reconstructs target bytes from an original source and a patch in a supported format.</summary>
public interface IDeltaApply
{
    /// <summary>Applies a patch and validates the reconstructed target checksum.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The encoded patch, including its output-length header and final checksum.</param>
    /// <returns>The reconstructed target in newly allocated memory and its verified checksum.</returns>
    /// <remarks>
    /// The built-in implementation allocates the length declared by the patch before validating all commands.
    /// Use <see cref="DeltaApplyExtensions.Apply"/> to impose an output-size limit.
    /// Processing stops at the first valid checksum command; trailing bytes are ignored.
    /// </remarks>
    /// <exception cref="DeltaApplyException">The patch structure, output length, source ranges, or checksum is invalid, or the patch is incomplete.</exception>
    /// <exception cref="DeltaReaderException">The format reader rejects malformed command syntax.</exception>
    DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta);

    /// <summary>Applies a patch into caller-provided memory and validates the reconstructed target checksum.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The encoded patch, including its output-length header and final checksum.</param>
    /// <param name="output">Writable memory at least as large as the declared target length.</param>
    /// <returns>A result whose data is the written prefix of <paramref name="output"/>, together with its verified checksum.</returns>
    /// <remarks>
    /// The result shares the caller's memory; subsequent changes to it affect the result.
    /// Bytes beyond the declared target length are left unchanged. Failure may leave partially written output.
    /// The built-in implementation supports overlapping source, patch, and output memory by copying overlapping inputs
    /// before writing. Processing stops at the first valid checksum command; trailing bytes are ignored.
    /// </remarks>
    /// <exception cref="DeltaApplyException">The output buffer is too small, or the patch structure, output length, source ranges, or checksum is invalid, or the patch is incomplete.</exception>
    /// <exception cref="DeltaReaderException">The format reader rejects malformed command syntax.</exception>
    DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, Memory<byte> output);

    /// <summary>Reads the target length declared in the first patch command without allocating an output buffer.</summary>
    /// <param name="delta">The encoded patch beginning with an output-length header.</param>
    /// <returns>The declared target length in bytes, which may exceed supported allocation sizes.</returns>
    /// <remarks>This only checks the first command; it does not validate the remaining patch or its checksum.</remarks>
    /// <exception cref="DeltaApplyException">The patch does not start with a valid output-length command.</exception>
    /// <exception cref="DeltaReaderException">The format reader rejects malformed header syntax.</exception>
    ulong GetOutputLength(ReadOnlySpan<byte> delta);
}

/// <summary>Applies patches using a configurable format reader and target checksum algorithm.</summary>
/// <typeparam name="TReader">The command reader for the patch format, with a public parameterless constructor.</typeparam>
/// <typeparam name="TChecksum">The checksum algorithm used when the patch was created, with a public parameterless constructor.</typeparam>
/// <remarks>Reader and checksum instances are reused across calls; their implementations determine whether concurrent use is safe.</remarks>
public class DeltaApply<TReader, TChecksum> : IDeltaApply
    where TReader : IDeltaReader, new()
    where TChecksum : IDeltaChecksum, new()
{
    TReader _reader;
    TChecksum _checksum;

    /// <summary>Initializes an applier with new reader and checksum instances.</summary>
    public DeltaApply()
    {
        _reader = new();
        _checksum = new();
    }

    /// <summary>Initializes an applier using the supplied reader and checksum instances.</summary>
    /// <param name="reader">The non-null reader used to decode commands; retained for subsequent calls.</param>
    /// <param name="checksum">The non-null checksum implementation; retained for subsequent calls.</param>
    public DeltaApply(TReader reader, TChecksum checksum)
    {
        _reader = reader;
        _checksum = checksum;
    }

    /// <inheritdoc cref="IDeltaApply.GetOutputLength"/>
    public ulong GetOutputLength(ReadOnlySpan<byte> delta)
    {
        if (!_reader.TryRead(delta, out var cmd, out var consumed))
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

        if (cmd.Command != DeltaCommandEnum.Length || consumed == 0 || consumed > (uint)delta.Length)
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

        return cmd.Length;
    }

    /// <inheritdoc cref="IDeltaApply.Apply(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Memory{byte})"/>
    public DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, Memory<byte> output)
    {
        var outputLength = GetOutputLength(delta);

        if (outputLength > int.MaxValue)
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidOutputLength);

        if ((ulong)output.Length < outputLength)
            throw new DeltaApplyException(DeltaApplyExceptionError.OutputMemorySizeLessThanRequired);

        var destination = output.Span.Slice(0, (int)outputLength);
        // Preserve unread input when the caller reuses either input buffer.
        if (source.Overlaps(destination))
            source = source.ToArray();
        if (delta.Overlaps(destination))
            delta = delta.ToArray();

        var applyResult = Apply(source, delta, destination, out var checksum);

        return new DeltaResult()
        {
            Crc32 = checksum,
            Data = output.Slice(0,applyResult.Length)
        };
    }

    /// <inheritdoc cref="IDeltaApply.Apply(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
    public DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        var outputLength = GetOutputLength(delta);

        if (outputLength > int.MaxValue)
            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidOutputLength);

#if NETSTANDARD2_0
        var buffer = new byte[(int)outputLength];
#else
        var buffer = GC.AllocateUninitializedArray<byte>((int)outputLength);
#endif

        var output = Apply(source, delta, buffer, out var checksum);

        return new DeltaResult()
        {
            Crc32 = checksum,
            Data = buffer
        };
    }

    private Span<byte> Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, Span<byte> outputBuffer, out uint checksum)
    {
        var outputSpan = outputBuffer;
        var writeSpan = outputSpan;
        var hasLength = false;

        while (_reader.TryRead(delta, out var cmd, out var consumed))
        {
            if (consumed == 0 || consumed > (uint)delta.Length ||
                (!hasLength && cmd.Command != DeltaCommandEnum.Length))
                throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

            switch (cmd.Command)
            {
                case DeltaCommandEnum.Length:
                    {
                        if (hasLength)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);

                        if (cmd.Length != (ulong)outputBuffer.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidOutputLength);

                        hasLength = true;

                        writeSpan = writeSpan.Slice(0, (int)cmd.Length);
                        outputSpan = writeSpan;

                        delta = delta.Slice((int)consumed);

                        break;
                    }
                case DeltaCommandEnum.Insert:
                    {
                        if (cmd.Length > (ulong)writeSpan.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidInsertLength);

                        delta = delta.Slice((int)consumed);

                        // The declared insert length must not exceed the bytes actually
                        // present in the (untrusted) delta tail, otherwise the Slice below
                        // throws ArgumentOutOfRangeException on malformed input.
                        if (cmd.Length > (ulong)delta.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidInsertLength);

                        var insertSlice = delta.Slice(0, (int)cmd.Length);

                        insertSlice.CopyTo(writeSpan);

                        delta = delta.Slice((int)cmd.Length);

                        writeSpan = writeSpan.Slice((int)cmd.Length);

                        break;
                    }
                case DeltaCommandEnum.Copy:
                    {
                        if (cmd.Position > (ulong)source.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidCopyPosition);

                        if (cmd.Length > (ulong)writeSpan.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidCopyLength);

                        // Position alone being in range is not enough: the copied region
                        // (Position + Length) must also stay within the source, otherwise
                        // the Slice below reads past the source end on a crafted delta.
                        // Compared in ulong space to avoid 32-bit overflow of the sum.
                        if (cmd.Position + cmd.Length > (ulong)source.Length)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidCopyLength);

                        delta = delta.Slice((int)consumed);

                        var copySlice = source.Slice((int)cmd.Position, (int)cmd.Length);
                        copySlice.CopyTo(writeSpan);

                        writeSpan = writeSpan.Slice((int)cmd.Length);

                        break;
                    }
                case DeltaCommandEnum.Checksum:
                    {
                        // Every output byte must come from a copy or insert command.
                        // A checksum of existing/uninitialized buffer contents is not enough.
                        if (!writeSpan.IsEmpty)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidOutputLength);

                        delta = delta.Slice((int)consumed);

                        var deltaChecksum = cmd.Length;

                        var outputChecksum = _checksum.Checksum(outputSpan);

                        if (deltaChecksum != outputChecksum)
                            throw new DeltaApplyException(DeltaApplyExceptionError.InvalidChecksum);

                        checksum = outputChecksum;

                        return outputSpan;

                    }
                default:
                    throw new DeltaApplyException(DeltaApplyExceptionError.InvalidDelta);
            }
        }

        throw new DeltaApplyException(DeltaApplyExceptionError.IncompleteDelta);
    }


}

/// <summary>Reports a patch validation or output-size failure during delta application.</summary>
/// <remarks>Inspect <see cref="Error"/> for the specific failure; command parsing may instead throw <see cref="DeltaReaderException"/>.</remarks>
public class DeltaApplyException : Exception
{
    /// <summary>Gets the validation failure that caused the exception.</summary>
    public DeltaApplyExceptionError Error { get; private set; }

    /// <summary>Initializes an exception for the specified application failure.</summary>
    /// <param name="error">The validation failure to report.</param>
    public DeltaApplyException(DeltaApplyExceptionError error)
    {
        Error = error;
    }

    /// <summary>Identifies validation failures reported while applying a patch.</summary>
    public enum DeltaApplyExceptionError
    {
        /// <summary>The command stream has an invalid header, command order, command kind, or consumed-byte count.</summary>
        InvalidDelta,
        /// <summary>The declared target length is unsupported or differs from the number of bytes reconstructed.</summary>
        InvalidOutputLength,
        /// <summary>The caller-provided output memory is smaller than the declared target length.</summary>
        OutputMemorySizeLessThanRequired,
        /// <summary>An insert exceeds the remaining output space or available patch payload.</summary>
        InvalidInsertLength,
        /// <summary>A copy starts beyond the end of the original source.</summary>
        InvalidCopyPosition,
        /// <summary>A copy extends beyond the original source or remaining output space.</summary>
        InvalidCopyLength,
        /// <summary>The reconstructed target checksum differs from the checksum in the patch.</summary>
        InvalidChecksum,
        /// <summary>Reading stopped before a terminating checksum command was processed.</summary>
        IncompleteDelta,
        /// <summary>The declared target length exceeds the caller's output-size limit.</summary>
        OutputLengthLimitExceeded

    }
}
