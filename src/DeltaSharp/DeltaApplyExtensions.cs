namespace DeltaSharp;

/// <summary>Provides output-size-limited patch application for <see cref="IDeltaApply"/> implementations.</summary>
public static class DeltaApplyExtensions
{
    /// <summary>Applies a patch after enforcing an output-size limit in bytes.</summary>
    /// <param name="apply">The applier for the patch's format and checksum algorithm.</param>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The encoded patch, including its output-length header and final checksum.</param>
    /// <param name="maxOutputLength">The maximum permitted target length in bytes; zero permits only empty output.</param>
    /// <returns>The reconstructed target in newly allocated memory and its verified checksum.</returns>
    /// <remarks>
    /// The declared length is checked before allocating the output buffer. The limit bounds the target
    /// allocation, not total memory usage or processing time. Validation is delegated to
    /// <see cref="IDeltaApply.Apply(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Memory{byte})"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="apply"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputLength"/> is negative.</exception>
    /// <exception cref="DeltaApplyException">
    /// The declared length exceeds the limit, with <see cref="DeltaApplyException.Error"/> set to
    /// <see cref="DeltaApplyException.DeltaApplyExceptionError.OutputLengthLimitExceeded"/>,
    /// or the applier rejects the patch during validation.
    /// </exception>
    /// <exception cref="Format.DeltaReaderException">The format reader rejects malformed command syntax.</exception>
    public static DeltaResult Apply(this IDeltaApply apply, ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> delta, int maxOutputLength)
    {
        if (apply is null)
            throw new ArgumentNullException(nameof(apply));
        if (maxOutputLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxOutputLength));

        var outputLength = apply.GetOutputLength(delta);
        if (outputLength > (ulong)maxOutputLength)
            throw new DeltaApplyException(DeltaApplyException.DeltaApplyExceptionError.OutputLengthLimitExceeded);

#if NETSTANDARD2_0
        var buffer = new byte[(int)outputLength];
#else
        var buffer = GC.AllocateUninitializedArray<byte>((int)outputLength);
#endif
        return apply.Apply(source, delta, buffer.AsMemory());
    }
}
