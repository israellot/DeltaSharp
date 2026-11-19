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
    public static int TableSize(int blockCount)
    {
#if NETSTANDARD2_0
        int size = 1;
        while (size < blockCount)
            size <<= 1;
        return blockCount == 0 ? 0 : size;
#else
        return (int)BitOperations.RoundUpToPowerOf2((uint)blockCount);
#endif
    }

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