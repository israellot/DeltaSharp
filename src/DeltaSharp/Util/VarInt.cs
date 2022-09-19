using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace DeltaSharp.Util;

/// <summary>
/// Implements Variable-Length Integers	 as described in 
/// https://sqlite.org/src4/doc/trunk/www/varint.wiki
/// </summary>
/// 
internal static class VarIntFormatter
{

    public static bool TryEncode(ulong value, Span<byte> buffer, out int written)
    {
        if (buffer.Length == 0)
        {
            written = 0;
            return false;
        }

        switch (value)
        {
            case <= 240:
                {
                    //If V<=240 then output a single by A0 equal to V.

                    buffer[0] = (byte)value;
                    written = 1;
                    return true;
                }
            case <= 2287:
                {
                    //If V<=2287 then output A0 as (V-240)/256 + 241 and A1 as (V-240)%256.

                    if (buffer.Length <= 1)
                    {
                        written = 0;
                        return false;
                    }

                    buffer[0] = (byte)(((value - 240) / 256) + 241);
                    buffer[1] = (byte)((value - 240) % 256);
                    written = 2;
                    return true;
                }
            case <= 67823:
                {
                    //If V<=67823 then output A0 as 249, A1 as (V-2288)/256, and A2 as (V-2288)%256.

                    if (buffer.Length <= 2)
                    {
                        written = 0;
                        return false;
                    }

                    buffer[0] = 249;
                    buffer[1] = (byte)((value - 2288) / 256);
                    buffer[2] = (byte)((value - 2288) % 256);
                    written = 3;
                    return true;
                }
            case <= 16777215:
                {
                    //If V<=16777215 then output A0 as 250 and A1 through A3 as a big-endian 3-byte integer.

                    if (buffer.Length <= 3)
                    {
                        written = 0;
                        return false;
                    }

                    
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 250;

                    written = 4;

                    return true;
                }
            case <= 4294967295:
                {
                    //If V<=4294967295 then output A0 as 251 and A1..A4 as a big-ending 4-byte integer.

                    if (buffer.Length <= 4)
                    {
                        written = 0;
                        return false;
                    }

                    
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 251;

                    written = 5;

                    return true;
                }
            case <= 1099511627775:
                {
                    //If V<=1099511627775 then output A0 as 252 and A1..A5 as a big-ending 5-byte integer.

                    if (buffer.Length <= 5)
                    {
                        written = 0;
                        return false;
                    }

                    
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 252;

                    written = 6;

                    return true;
                }
            case <= 281474976710655:
                {
                    //If V<=281474976710655 then output A0 as 253 and A1..A6 as a big-ending 6-byte integer.

                    if (buffer.Length <= 6)
                    {
                        written = 0;
                        return false;
                    }

                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 253;

                    written = 7;

                    return true;
                }
            case <= 72057594037927935:
                {
                    //If V<=72057594037927935 then output A0 as 254 and A1..A7 as a big-ending 7-byte integer.

                    if (buffer.Length <= 7)
                    {
                        written = 0;
                        return false;
                    }

                    
                    buffer[7] = (byte)(value); value >>= 8;
                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 254;

                    written = 8;

                    return true;
                }
            default:
                {
                    //Otherwise then output A0 as 255 and A1..A8 as a big-ending 8-byte integer.

                    if (buffer.Length <= 8)
                    {
                        written = 0;
                        return false;
                    }

                   
                    buffer[8] = (byte)(value); value >>= 8;
                    buffer[7] = (byte)(value); value >>= 8;
                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 255;
                                        
                    written = 9;

                    return true;
                }

        }


    }

    public static int EncodeUnsafe(ulong value, Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        switch (value)
        {
            case <= 240:
                {
                    //If V<=240 then output a single by A0 equal to V.

                    buffer[0] = (byte)value;
                    return 1;
                }
            case <= 2287:
                {
                    //If V<=2287 then output A0 as (V-240)/256 + 241 and A1 as (V-240)%256.
                                        
                    buffer[0] = (byte)(((value - 240) / 256) + 241);
                    buffer[1] = (byte)((value - 240) % 256);
                    return 2;
                }
            case <= 67823:
                {
                    //If V<=67823 then output A0 as 249, A1 as (V-2288)/256, and A2 as (V-2288)%256.

                    buffer[0] = 249;
                    buffer[1] = (byte)((value - 2288) / 256);
                    buffer[2] = (byte)((value - 2288) % 256);
                    
                    return 3;
                }
            case <= 16777215:
                {
                    //If V<=16777215 then output A0 as 250 and A1 through A3 as a big-endian 3-byte integer.

                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 250;

                    return 4;
                }
            case <= 4294967295:
                {
                    //If V<=4294967295 then output A0 as 251 and A1..A4 as a big-ending 4-byte integer.

                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 251;

                    return 5;
                }
            case <= 1099511627775:
                {
                    //If V<=1099511627775 then output A0 as 252 and A1..A5 as a big-ending 5-byte integer.


                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 252;

                    return 6;
                }
            case <= 281474976710655:
                {
                    //If V<=281474976710655 then output A0 as 253 and A1..A6 as a big-ending 6-byte integer.

                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 253;

                    return 7;
                }
            case <= 72057594037927935:
                {
                    //If V<=72057594037927935 then output A0 as 254 and A1..A7 as a big-ending 7-byte integer.

                    buffer[7] = (byte)(value); value >>= 8;
                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 254;

                    return 8;
                }
            default:
                {
                    //Otherwise then output A0 as 255 and A1..A8 as a big-ending 8-byte integer.

                    buffer[8] = (byte)(value); value >>= 8;
                    buffer[7] = (byte)(value); value >>= 8;
                    buffer[6] = (byte)(value); value >>= 8;
                    buffer[5] = (byte)(value); value >>= 8;
                    buffer[4] = (byte)(value); value >>= 8;
                    buffer[3] = (byte)(value); value >>= 8;
                    buffer[2] = (byte)(value); value >>= 8;
                    buffer[1] = (byte)(value);
                    buffer[0] = 255;

                    return 9;
                }

        }


    }


    public static bool TryDecode(ReadOnlySpan<byte> buffer,out ulong value,out int consumed)
    {
        value = 0;
        consumed = 0;
        if (buffer.IsEmpty)
            return false;

        byte a0 = buffer[0];

        switch (a0)
        {
            case <= 240:
                {
                    //If A0 is between 0 and 240 inclusive, then the result is the value of A0.

                    value = (ulong)a0;
                    consumed = 1;
                    return true;
                }
            case <= 248:
                {
                    //If A0 is between 241 and 248 inclusive, then the result is 240 + 256 * (A0 - 241) + A1.

                    if (buffer.Length <= 1)
                        return false;

                    var a1 = buffer[1];

                    value = 240UL + 256U*((uint)(a0 - 241)) + a1;

                    consumed = 2;

                    return true;
                }
            case 249:
                {
                    //If A0 is 249 then the result is 2288 + 256 * A1 + A2.

                    if (buffer.Length <= 2)
                        return false;

                    var a1 = buffer[1];
                    var a2 = buffer[2];

                    value = 2288UL + 256U * a1 + a2;

                    consumed = 3;

                    return true;
                }
            case 250:
                {
                    //If A0 is 250 then the result is A1..A3 as a 3 - byte big - ending integer.

                    if (buffer.Length <= 3)
                        return false;

                    Span<byte> temp = stackalloc byte[4];

                    buffer.Slice(1,3).CopyTo(temp.Slice(1));

                    value = BinaryPrimitives.ReadUInt32BigEndian(temp);

                    consumed = 4;

                    return true;
                }
            case 251:
                {
                    //If A0 is 251 then the result is A1..A4 as a 4 - byte big - ending integer.

                    if (buffer.Length <= 4)
                        return false;

                    Span<byte> temp = stackalloc byte[4];

                    buffer.Slice(1, 4).CopyTo(temp);

                    value = BinaryPrimitives.ReadUInt32BigEndian(temp);

                    consumed = 5;

                    return true;
                }
            case 252:
                {
                    //If A0 is 252 then the result is A1..A5 as a 5 - byte big - ending integer.

                    if (buffer.Length <= 5)
                        return false;

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 5).CopyTo(temp.Slice(3));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 6;

                    return true;
                }
            case 253:
                {
                    //If A0 is 253 then the result is A1..A6 as a 6 - byte big - ending integer.

                    if (buffer.Length <= 6)
                        return false;

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 6).CopyTo(temp.Slice(2));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 7;

                    return true;
                }
            case 254:
                {
                    //If A0 is 254 then the result is A1..A7 as a 7 - byte big - ending integer.

                    if (buffer.Length <= 7)
                        return false;

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 7).CopyTo(temp.Slice(1));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 8;

                    return true;
                }
            case 255:
                {
                    //If A0 is 255 then the result is A1..A8 as a 8 - byte big - ending integer.

                    if (buffer.Length <= 8)
                        return false;

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 8).CopyTo(temp);

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 9;

                    return true;
                }
        }
    }

    public static ulong DecodeUnsafe(ReadOnlySpan<byte> buffer, out int consumed)
    {
        ulong value = 0;
        consumed = 0;
        if (buffer.IsEmpty)
            return value;

        byte a0 = buffer[0];

        switch (a0)
        {
            case <= 240:
                {
                    //If A0 is between 0 and 240 inclusive, then the result is the value of A0.

                    value = (ulong)a0;
                    consumed = 1;
                    return value;
                }
            case <= 248:
                {
                    //If A0 is between 241 and 248 inclusive, then the result is 240 + 256 * (A0 - 241) + A1.                                       

                    var a1 = buffer[1];

                    value = 240UL + 256U * ((uint)(a0 - 241)) + a1;

                    consumed = 2;

                    return value;
                }
            case 249:
                {
                    //If A0 is 249 then the result is 2288 + 256 * A1 + A2.

                    var a1 = buffer[1];
                    var a2 = buffer[2];

                    value = 2288UL + 256U * a1 + a2;

                    consumed = 3;

                    return value;
                }
            case 250:
                {
                    //If A0 is 250 then the result is A1..A3 as a 3 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[4];

                    buffer.Slice(1, 3).CopyTo(temp.Slice(1));

                    value = BinaryPrimitives.ReadUInt32BigEndian(temp);

                    consumed = 4;

                    return value;
                }
            case 251:
                {
                    //If A0 is 251 then the result is A1..A4 as a 4 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[4];

                    buffer.Slice(1, 4).CopyTo(temp);

                    value = BinaryPrimitives.ReadUInt32BigEndian(temp);

                    consumed = 5;

                    return value;
                }
            case 252:
                {
                    //If A0 is 252 then the result is A1..A5 as a 5 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 5).CopyTo(temp.Slice(3));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 6;

                    return value;
                }
            case 253:
                {
                    //If A0 is 253 then the result is A1..A6 as a 6 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 6).CopyTo(temp.Slice(2));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 7;

                    return value;
                }
            case 254:
                {
                    //If A0 is 254 then the result is A1..A7 as a 7 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 7).CopyTo(temp.Slice(1));

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 8;

                    return value;
                }
            case 255:
                {
                    //If A0 is 255 then the result is A1..A8 as a 8 - byte big - ending integer.

                    Span<byte> temp = stackalloc byte[8];

                    buffer.Slice(1, 8).CopyTo(temp);

                    value = BinaryPrimitives.ReadUInt64BigEndian(temp);

                    consumed = 9;

                    return value;
                }
        }
    }


    public static byte[] Encode(ulong value)
    {
        Span<byte> buffer=stackalloc byte[9];

        TryEncode(value, buffer, out var written);

        return buffer.Slice(0, written).ToArray();
    }
}
