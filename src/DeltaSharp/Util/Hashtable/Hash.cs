using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util.Hashtable;

internal readonly ref struct Hash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Hash64(ulong x)
    {
        //  SplitableRandom 
        // https://nullprogram.com/blog/2018/07/31/
        unchecked
        {
            x ^= x >> 30;
            x *= 0xbf58476d1ce4e5b9U;
            x ^= x >> 27;
            x *= 0x94d049bb133111ebU;
            x ^= x >> 31;

            return x;
        }

    }

}