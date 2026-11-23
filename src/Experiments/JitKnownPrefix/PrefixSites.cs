using System.Runtime.CompilerServices;

// NoInlining keeps these public-user call sites independently visible in a JIT dump.
// The MemoryExtensions call and all span construction inside each site may inline.
public static class PrefixSites
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Both4(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..4].CommonPrefixLength(b[..4]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Both8(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..8].CommonPrefixLength(b[..8]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Both16(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..16].CommonPrefixLength(b[..16]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FourEight(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..4].CommonPrefixLength(b[..8]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int EightFour(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..8].CommonPrefixLength(b[..4]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SixteenEight(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..16].CommonPrefixLength(b[..8]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FixedDynamic(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..8].CommonPrefixLength(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DynamicFixed(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a.CommonPrefixLength(b[..8]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Unknown(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a.CommonPrefixLength(b);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Both7(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a[..7].CommonPrefixLength(b[..7]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FourUShort(ReadOnlySpan<ushort> a, ReadOnlySpan<ushort> b)
        => a[..4].CommonPrefixLength(b[..4]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int TwoUInt(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
        => a[..2].CommonPrefixLength(b[..2]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int OneULong(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
        => a[..1].CommonPrefixLength(b[..1]);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int TwoFloat(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        => a[..2].CommonPrefixLength(b[..2]);
}
