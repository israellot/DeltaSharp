using DeltaSharp.Format;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

    public DeltaFossil()
    {
        Apply = new DeltaApply<FossilDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<FossilDeltaWriter, DeltaChecksum>();
    }

}

public class DeltaBinary : IDelta
{
    public IDeltaApply Apply { get; }
    public IDeltaCreate Create { get; }

    public DeltaBinary()
    {
        Apply = new DeltaApply<BinaryDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>();
    }
}

public class DeltaASCII : IDelta
{
    public IDeltaApply Apply { get; }
    public IDeltaCreate Create { get; }

    public DeltaASCII()
    {
        Apply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();
        Create = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>();
    }
}

public static class DeltaSharp
{
    public static IDelta Binary { get; }
    public static IDelta Fossil { get; }
    public static IDelta ASCII { get; }

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

    public static DeltaResult CreateFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return Fossil.Create.Create(source, target);
    }

    public static DeltaResult CreateASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target)
    {
        return ASCII.Create.Create(source, target);
    }

    public static DeltaResult ApplyBinary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Binary.Apply.Apply(source, delta);
    }

    public static DeltaResult ApplyFossil(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return Fossil.Apply.Apply(source, delta);
    }

    public static DeltaResult ApplyASCII(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
    {
        return ASCII.Apply.Apply(source, delta);
    }
}