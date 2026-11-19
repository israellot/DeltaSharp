using DeltaSharp.Format;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static DeltaSharp.DeltaApplyException;

namespace DeltaSharp;


/// <summary>Provides matching patch creation and application services for a delta format.</summary>
public interface IDelta
{
    /// <summary>Gets the service that applies patches in this format.</summary>
    IDeltaApply Apply { get; }
    /// <summary>Gets the service that creates patches in this format.</summary>
    IDeltaCreate Create { get; }
}

/// <summary>Creates and applies Fossil-format patches using the Fossil additive checksum.</summary>
public class DeltaFossil: IDelta
{
    /// <inheritdoc cref="IDelta.Apply"/>
    public IDeltaApply Apply { get; }
    /// <inheritdoc cref="IDelta.Create"/>
    public IDeltaCreate Create { get; }

    /// <summary>Initializes Fossil services with exhaustive source-match search for patch creation.</summary>
    public DeltaFossil() : this(DeltaSearchMode.Exhaustive)
    {
    }

    /// <summary>Initializes Fossil services with the specified source-match search mode for patch creation.</summary>
    /// <param name="searchMode">The search-effort policy for the creation service; does not affect patch application.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public DeltaFossil(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<FossilDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<FossilDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }

}

/// <summary>Creates and applies DeltaSharp binary patches using the Fossil additive checksum.</summary>
public class DeltaBinary : IDelta
{
    /// <inheritdoc cref="IDelta.Apply"/>
    public IDeltaApply Apply { get; }
    /// <inheritdoc cref="IDelta.Create"/>
    public IDeltaCreate Create { get; }

    /// <summary>Initializes binary services with exhaustive source-match search for patch creation.</summary>
    public DeltaBinary() : this(DeltaSearchMode.Exhaustive)
    {
    }

    /// <summary>Initializes binary services with the specified source-match search mode for patch creation.</summary>
    /// <param name="searchMode">The search-effort policy for the creation service; does not affect patch application.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public DeltaBinary(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<BinaryDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }
}

/// <summary>Creates and applies DeltaSharp ASCII patches using the Fossil additive checksum.</summary>
/// <remarks>Command headers are ASCII text, but inserted payloads may contain arbitrary binary bytes.</remarks>
public class DeltaASCII : IDelta
{
    /// <inheritdoc cref="IDelta.Apply"/>
    public IDeltaApply Apply { get; }
    /// <inheritdoc cref="IDelta.Create"/>
    public IDeltaCreate Create { get; }

    /// <summary>Initializes ASCII services with exhaustive source-match search for patch creation.</summary>
    public DeltaASCII() : this(DeltaSearchMode.Exhaustive)
    {
    }

    /// <summary>Initializes ASCII services with the specified source-match search mode for patch creation.</summary>
    /// <param name="searchMode">The search-effort policy for the creation service; does not affect patch application.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public DeltaASCII(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }
}

/// <summary>Provides shared services and convenience methods for binary, ASCII, and Fossil patches.</summary>
/// <remarks>
/// All built-in formats use the Fossil additive target checksum. Match the creation and application
/// formats, and supply the original source when applying a patch. Creation defaults to
/// <see cref="DeltaSearchMode.Exhaustive"/>; use an explicit mode to bound source-match candidates.
/// </remarks>
public static class DeltaSharp
{
    /// <summary>Gets shared binary-format services with exhaustive source-match search for creation.</summary>
    public static IDelta Binary { get; }
    /// <summary>Gets shared Fossil-format services with exhaustive source-match search for creation.</summary>
    public static IDelta Fossil { get; }
    /// <summary>Gets shared ASCII-format services with exhaustive source-match search for creation.</summary>
    public static IDelta ASCII { get; }

    private static readonly IDeltaCreate BoundedBinaryCreator =
        new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum(), DeltaSearchMode.Bounded);
    private static readonly IDeltaCreate BoundedFossilCreator =
        new DeltaCreate<FossilDeltaWriter, DeltaChecksum>(new DeltaChecksum(), DeltaSearchMode.Bounded);
    private static readonly IDeltaCreate BoundedAsciiCreator =
        new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>(new DeltaChecksum(), DeltaSearchMode.Bounded);

    static DeltaSharp()
    {
        Binary = new DeltaBinary();
        Fossil = new DeltaFossil();
        ASCII = new DeltaASCII();
    }

    /// <summary>Creates a binary patch using exhaustive source-match search.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <returns>The binary patch bytes and the Fossil additive checksum of the target.</returns>
    public static DeltaResult CreateBinary(ReadOnlySpan<byte> source,ReadOnlySpan<byte> target)
    {
        return  Binary.Create.Create(source, target);
    }

    /// <summary>Creates a binary patch using the selected source-match search mode.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <param name="searchMode">The search-effort policy for this call.</param>
    /// <returns>The binary patch bytes and the Fossil additive checksum of the target.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public static DeltaResult CreateBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, Binary.Create, BoundedBinaryCreator).Create(source, target);

    /// <summary>Creates a Fossil patch using exhaustive source-match search.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <returns>The Fossil patch bytes and the Fossil additive checksum of the target.</returns>
    public static DeltaResult CreateFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return Fossil.Create.Create(source, target);
    }

    /// <summary>Creates a Fossil patch using the selected source-match search mode.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <param name="searchMode">The search-effort policy for this call.</param>
    /// <returns>The Fossil patch bytes and the Fossil additive checksum of the target.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public static DeltaResult CreateFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, Fossil.Create, BoundedFossilCreator).Create(source, target);

    /// <summary>Creates an ASCII patch using exhaustive source-match search.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <returns>The ASCII patch bytes, including raw insert payloads, and the Fossil additive checksum of the target.</returns>
    public static DeltaResult CreateASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return ASCII.Create.Create(source, target);
    }

    /// <summary>Creates an ASCII patch using the selected source-match search mode.</summary>
    /// <param name="source">The original bytes that will be available when applying the patch; may be empty.</param>
    /// <param name="target">The desired reconstructed bytes; may be empty.</param>
    /// <param name="searchMode">The search-effort policy for this call.</param>
    /// <returns>The ASCII patch bytes, including raw insert payloads, and the Fossil additive checksum of the target.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchMode"/> is not a defined search mode.</exception>
    public static DeltaResult CreateASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, ASCII.Create, BoundedAsciiCreator).Create(source, target);

    private static IDeltaCreate SelectCreator(DeltaSearchMode searchMode, IDeltaCreate exhaustive, IDeltaCreate bounded)
        => searchMode switch
        {
            DeltaSearchMode.Exhaustive => exhaustive,
            DeltaSearchMode.Bounded => bounded,
            _ => throw new ArgumentOutOfRangeException(nameof(searchMode), searchMode, "Unknown search mode.")
        };

    /// <summary>Applies a binary patch and verifies the target checksum.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The binary patch to apply.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>Allocates the declared target length before full validation. Use the overload with a size limit to cap this allocation. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="DeltaApplyException">The patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The binary reader rejects malformed command syntax.</exception>
    public static DeltaResult ApplyBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Binary.Apply.Apply(source, delta);
    }

    /// <summary>Applies a binary patch with an output-size limit checked before allocation.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The binary patch to apply.</param>
    /// <param name="maxOutputLength">The maximum target length in bytes; zero permits only empty output.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>The limit bounds the target allocation, not total memory or processing time. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputLength"/> is negative.</exception>
    /// <exception cref="DeltaApplyException">The declared length exceeds the limit, or the patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The binary reader rejects malformed command syntax.</exception>
    public static DeltaResult ApplyBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => Binary.Apply.Apply(source, delta, maxOutputLength);

    /// <summary>Applies a Fossil patch and verifies the target checksum.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The Fossil patch to apply.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>Allocates the declared target length before full validation. Use the overload with a size limit to cap this allocation. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="DeltaApplyException">The patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The Fossil reader rejects malformed syntax or a numeric operand exceeding 32 bits.</exception>
    public static DeltaResult ApplyFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Fossil.Apply.Apply(source, delta);
    }

    /// <summary>Applies a Fossil patch with an output-size limit checked before allocation.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The Fossil patch to apply.</param>
    /// <param name="maxOutputLength">The maximum target length in bytes; zero permits only empty output.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>The limit bounds the target allocation, not total memory or processing time. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputLength"/> is negative.</exception>
    /// <exception cref="DeltaApplyException">The declared length exceeds the limit, or the patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The Fossil reader rejects malformed syntax or a numeric operand exceeding 32 bits.</exception>
    public static DeltaResult ApplyFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => Fossil.Apply.Apply(source, delta, maxOutputLength);

    /// <summary>Applies an ASCII patch and verifies the target checksum.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The ASCII patch to apply, including any raw insert payloads.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>Allocates the declared target length before full validation. Use the overload with a size limit to cap this allocation. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="DeltaApplyException">The patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The ASCII reader rejects malformed command syntax.</exception>
    public static DeltaResult ApplyASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return ASCII.Apply.Apply(source, delta);
    }

    /// <summary>Applies an ASCII patch with an output-size limit checked before allocation.</summary>
    /// <param name="source">The original source bytes against which the patch was created.</param>
    /// <param name="delta">The ASCII patch to apply, including any raw insert payloads.</param>
    /// <param name="maxOutputLength">The maximum target length in bytes; zero permits only empty output.</param>
    /// <returns>The reconstructed target in newly allocated memory and its Fossil additive checksum.</returns>
    /// <remarks>The limit bounds the target allocation, not total memory or processing time. Bytes after the first valid checksum command are ignored.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputLength"/> is negative.</exception>
    /// <exception cref="DeltaApplyException">The declared length exceeds the limit, or the patch is incomplete or fails structural, length, source-range, or checksum validation.</exception>
    /// <exception cref="DeltaReaderException">The ASCII reader rejects malformed command syntax.</exception>
    public static DeltaResult ApplyASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => ASCII.Apply.Apply(source, delta, maxOutputLength);
}
