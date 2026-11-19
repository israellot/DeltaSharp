using DeltaSharp.Format;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static DeltaSharp.DeltaApplyException;

namespace DeltaSharp;


public interface IDelta
{
    IDeltaApply Apply { get; }
    IDeltaCreate Create { get; }
}

public class DeltaFossil: IDelta
{
    public IDeltaApply Apply { get; }
    public IDeltaCreate Create { get; }

    public DeltaFossil() : this(DeltaSearchMode.Exhaustive)
    {
    }

    public DeltaFossil(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<FossilDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<FossilDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }

}

public class DeltaBinary : IDelta
{
    public IDeltaApply Apply { get; }
    public IDeltaCreate Create { get; }

    public DeltaBinary() : this(DeltaSearchMode.Exhaustive)
    {
    }

    public DeltaBinary(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<BinaryDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }
}

public class DeltaASCII : IDelta
{
    public IDeltaApply Apply { get; }
    public IDeltaCreate Create { get; }

    public DeltaASCII() : this(DeltaSearchMode.Exhaustive)
    {
    }

    public DeltaASCII(DeltaSearchMode searchMode)
    {
        Apply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>(new DeltaChecksum(), searchMode);
    }
}

public static class DeltaSharp
{
    public static IDelta Binary { get; }
    public static IDelta Fossil { get; }
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

    public static DeltaResult CreateBinary(ReadOnlySpan<byte> source,ReadOnlySpan<byte> target)
    {
        return  Binary.Create.Create(source, target);
    }

    /// <summary>Creates a binary patch using the selected source-match search mode.</summary>
    public static DeltaResult CreateBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, Binary.Create, BoundedBinaryCreator).Create(source, target);

    public static DeltaResult CreateFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return Fossil.Create.Create(source, target);
    }

    /// <summary>Creates a Fossil patch using the selected source-match search mode.</summary>
    public static DeltaResult CreateFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, Fossil.Create, BoundedFossilCreator).Create(source, target);

    public static DeltaResult CreateASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return ASCII.Create.Create(source, target);
    }

    /// <summary>Creates an ASCII patch using the selected source-match search mode.</summary>
    public static DeltaResult CreateASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target, DeltaSearchMode searchMode)
        => SelectCreator(searchMode, ASCII.Create, BoundedAsciiCreator).Create(source, target);

    private static IDeltaCreate SelectCreator(DeltaSearchMode searchMode, IDeltaCreate exhaustive, IDeltaCreate bounded)
        => searchMode switch
        {
            DeltaSearchMode.Exhaustive => exhaustive,
            DeltaSearchMode.Bounded => bounded,
            _ => throw new ArgumentOutOfRangeException(nameof(searchMode), searchMode, "Unknown search mode.")
        };

    public static DeltaResult ApplyBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Binary.Apply.Apply(source, delta);
    }

    /// <summary>Applies a binary patch with an output-size limit checked before allocation.</summary>
    public static DeltaResult ApplyBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => Binary.Apply.Apply(source, delta, maxOutputLength);

    public static DeltaResult ApplyFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Fossil.Apply.Apply(source, delta);
    }

    /// <summary>Applies a Fossil patch with an output-size limit checked before allocation.</summary>
    public static DeltaResult ApplyFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => Fossil.Apply.Apply(source, delta, maxOutputLength);

    public static DeltaResult ApplyASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return ASCII.Apply.Apply(source, delta);
    }

    /// <summary>Applies an ASCII patch with an output-size limit checked before allocation.</summary>
    public static DeltaResult ApplyASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, int maxOutputLength)
        => ASCII.Apply.Apply(source, delta, maxOutputLength);
}
