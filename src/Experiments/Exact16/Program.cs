using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (args.SequenceEqual(new[] { "--verify" }) || args.SequenceEqual(new[] { "--jit" }))
{
    var checks = 0;
    var rng = new Random(16);
    var a = new byte[40];
    var b = new byte[40];
    rng.NextBytes(a);
    int rounds = args[0] == "--jit" ? 16 : 1;
    for (int round = 0; round < rounds; round++)
    {
        if (rounds > 1) Thread.Sleep(50);
        for (int x = 0; x < 8; x++)
        for (int y = 0; y < 8; y++)
        for (int mismatch = 0; mismatch <= 16; mismatch++)
        for (int bit = 0; bit < 8; bit++)
        {
            a.AsSpan(x, 16).CopyTo(b.AsSpan(y));
            if (mismatch < 16) b[y + mismatch] ^= (byte)(1 << bit);
            b[y + 16] = (byte)(a[x + 16] ^ 0xff);
            var left = a.AsSpan(x);
            var right = b.AsSpan(y);
            if (Stock.Match(left, right) != mismatch || Words.Match(left, right) != mismatch ||
                VectorMask.Match(left, right) != mismatch || VectorWords.Match(left, right) != mismatch)
                throw new Exception("Prefix mismatch");
            checks += 4;
        }
    }

    Console.WriteLine($"{checks} checks passed on {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}, Vector128={Vector128.IsHardwareAccelerated}");
    return 0;
}

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Known16).Assembly).Run(args).ToArray();
return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.Any() && s.Reports.All(r => r.Success)) ? 0 : 1;

public interface IKernel
{
    static abstract int Match(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y);
}

public readonly struct Stock : IKernel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Match(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y) =>
        x[..16].CommonPrefixLength(y[..16]);
}

public static class Bits
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int First(ulong difference) =>
        (BitConverter.IsLittleEndian ? BitOperations.TrailingZeroCount(difference) : BitOperations.LeadingZeroCount(difference)) >> 3;
}

public readonly struct Words : IKernel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Match(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        ref byte a = ref MemoryMarshal.GetReference(x[..16]);
        ref byte b = ref MemoryMarshal.GetReference(y[..16]);
        ulong first = Unsafe.ReadUnaligned<ulong>(ref a) ^ Unsafe.ReadUnaligned<ulong>(ref b);
        if (first != 0) return Bits.First(first);
        ulong last = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref a, 8)) ^
                     Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref b, 8));
        return 8 + Bits.First(last);
    }
}

public readonly struct VectorMask : IKernel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Match(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var a = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(x[..16]));
        var b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(y[..16]));
        uint equal = Vector128.Equals(a, b).ExtractMostSignificantBits();
        // Only the low 16 bits of equal can be set. Bit 16 of ~equal acts as the all-equal sentinel.
        return BitOperations.TrailingZeroCount(~equal);
    }
}

public readonly struct VectorWords : IKernel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Match(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var a = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(x[..16]));
        var b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(y[..16]));
        var different = (a ^ b).AsUInt64();
        ulong first = different.GetElement(0);
        if (first != 0) return Bits.First(first);
        return 8 + Bits.First(different.GetElement(1));
    }
}

public class Known16
{
    [Params("first", "index7", "index8", "last", "equal", "mixed")]
    public string Input { get; set; } = "";

    private const int Samples = 256;
    private (byte[] X, byte[] Y)[] pairs = [];

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(71237);
        pairs = new (byte[], byte[])[Samples];
        for (int i = 0; i < Samples; i++)
        {
            var x = new byte[17];
            var y = new byte[19];
            rng.NextBytes(x);
            x.AsSpan(1, 16).CopyTo(y.AsSpan(3));
            int index = Input switch
            {
                "first" => 0, "index7" => 7, "index8" => 8, "last" => 15,
                "equal" => 16, "mixed" => rng.Next(17), _ => throw new ArgumentException(Input)
            };
            if (index < 16) y[3 + index] ^= 1;
            pairs[i] = (x, y);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Run<T>() where T : struct, IKernel
    {
        int sum = 0;
        foreach (var pair in pairs) sum += T.Match(pair.X.AsSpan(1), pair.Y.AsSpan(3));
        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Samples)]
    public int Current() => Run<Stock>();
    [Benchmark(OperationsPerInvoke = Samples)] public int TwoWords() => Run<Words>();
    [Benchmark(OperationsPerInvoke = Samples)] public int CompareMask() => Run<VectorMask>();
    [Benchmark(OperationsPerInvoke = Samples)] public int XorLanes() => Run<VectorWords>();
}
