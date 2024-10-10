using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;


internal readonly ref struct HashtableStats
{
    public readonly int Collisions { get; init; }
    public readonly int Slots { get; init; }
    public readonly int SlotsFilled { get; init; }
}