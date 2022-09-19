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

public static class DeltaSharp
{
    public static IDelta Binary { get; }
    public static IDelta Fossil { get; }

    static DeltaSharp()
    {
        Binary = new DeltaBinary();
        Fossil = new DeltaFossil();
    }
}