namespace DeltaSharp;

public static class DeltaApplyExtensions
{
    /// <summary>Applies a patch after enforcing an output-size limit in bytes.</summary>
    /// <remarks>
    /// The limit is checked before allocating the output buffer. Zero permits only
    /// empty output. This extension preserves compatibility with existing IDeltaApply
    /// implementations and uses their caller-provided-buffer validation path.
    /// </remarks>
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
