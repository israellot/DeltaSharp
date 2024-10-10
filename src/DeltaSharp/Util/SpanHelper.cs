using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util;
internal class SpanHelper
{
    public static int MatchForward(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var length = Math.Min(x.Length, y.Length);

        if (!Vector128.IsHardwareAccelerated || length < 16)
        {
            ref byte xPtr = ref MemoryMarshal.GetReference(x);
            ref byte yPtr = ref MemoryMarshal.GetReference(y);

            int i = 0;
            ulong mask = 0;

            for (; i < length - 8; i = i + 8)
            {
                var xlong = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref xPtr, i));
                var ylong = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref yPtr, i));
                mask = (xlong ^ ylong);

                if (mask != 0)
                {
                    return (int)(i + (int)ulong.TrailingZeroCount(mask) / 8);
                }
            }

            for (; i < length; i++)
            {
                if (Unsafe.Add(ref xPtr, i) != Unsafe.Add(ref yPtr, i))
                    break;
            }

            return i;
        }
        else
        {
            uint mask;
            nuint lengthToExamine = (nuint)length - 16;

            Vector128<byte> maskVec;
            nuint i = 0;

            while (i < lengthToExamine)
            {
                maskVec = Vector128.Equals(
                    Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(x), i),
                    Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(y), i));

                mask = maskVec.ExtractMostSignificantBits();
                if (mask != 0xFFFF)
                    goto Found;

                i += 16;
            }

            {
                // Do final compare from end rather than start
                // This can overlap with already analyzed bytes on the previous iteration, but not an issue
                i = lengthToExamine;
                maskVec = Vector128.Equals(
                    Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(x), i),
                    Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(y), i));

                mask = maskVec.ExtractMostSignificantBits();
                if (mask != 0xFFFF)
                    goto Found;
            }

            return length;

        Found:
            mask = ~mask;
            return (int)(i + uint.TrailingZeroCount(mask));
        }

    }

    public static int MatchBackward(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var length = Math.Min(x.Length, y.Length);

        

        if (!Vector128.IsHardwareAccelerated || length < 16)
        {
            ref byte xPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(x), x.Length - 1);
            ref byte yPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(y), y.Length - 1);

            int i = 0;
            ulong mask = 0;

            for (; i < length - 8; i = i + 8)
            {
                var xlong = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Subtract(ref xPtr, (i + 7)));
                var ylong = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Subtract(ref yPtr, (i + 7)));
                mask = (xlong ^ ylong);

                if (mask != 0)
                {
                    return (int)(i + (int)ulong.LeadingZeroCount(mask) / 8);
                }
            }

            for (; i < length; i++)
            {
                if (Unsafe.Add(ref xPtr, -i) != Unsafe.Add(ref yPtr, -i))
                    break;
            }

            return i;
        }
        else// Vector128.IsHardwareAccelerated
        {
            uint mask;

            Vector128<byte> maskVec;
            int i = length - 16;

            ref byte xPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(x), x.Length - length);
            ref byte yPtr = ref Unsafe.Add(ref MemoryMarshal.GetReference(y), y.Length - length);

            while (i > 0)
            {
                var vx = Vector128.LoadUnsafe(ref xPtr, (nuint)i);
                var vy = Vector128.LoadUnsafe(ref yPtr, (nuint)i);

                maskVec = Vector128.Equals(vx, vy);

                mask = maskVec.ExtractMostSignificantBits();
                if (mask != 0xFFFF)
                    goto Found;

                i -= 16;
            }

            {
                i = 0;

                // Do final compare from start rather than end
                // This can overlap with already analyzed bytes on the previous iteration, but not an issue
                var vx = Vector128.LoadUnsafe(ref xPtr,0);
                var vy = Vector128.LoadUnsafe(ref yPtr,0);

                maskVec = Vector128.Equals(vx, vy);

                mask = maskVec.ExtractMostSignificantBits();
                if (mask != 0xFFFF)
                    goto Found;
            }


            return length;

        Found:
            mask = ~mask;
            var result= (int)(length - i - 16 + ushort.LeadingZeroCount((ushort)mask));

            return result;
        }


    }

}
