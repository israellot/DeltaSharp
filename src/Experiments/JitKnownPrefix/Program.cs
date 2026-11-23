using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (args.SequenceEqual(new[] { "--verify" })) { Verify.Run(); return 0; }
if (args.SequenceEqual(new[] { "--jit" }))
{
    long total = 0;
    for (int i = 0; i < 10; i++)
    {
        Thread.Sleep(80);
        total += Verify.Heat();
    }
    Console.WriteLine(total);
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Fixed).Assembly).Run(args).ToArray();
return summaries.Length != 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.Any() && s.Reports.All(r => r.Success)) ? 0 : 1;

public class Fixed
{
    public const int Samples = 256;
    [Params("First", "Last", "Equal", "Varied")] public string Pattern { get; set; } = "Varied";
    private (byte[] A, byte[] B)[] four = [], eight = [], sixteen = [];
    [GlobalSetup]
    public void Setup() { four = Pairs(4); eight = Pairs(8); sixteen = Pairs(16); }

    private (byte[], byte[])[] Pairs(int n)
    {
        var rng = new Random(8719);
        return Enumerable.Range(0, Samples).Select(_ =>
        {
            var a = new byte[19]; var b = new byte[21]; rng.NextBytes(a);
            a.AsSpan(1, 16).CopyTo(b.AsSpan(2));
            int p = Pattern switch { "First" => 0, "Last" => n - 1, "Equal" => n, _ => rng.Next(n + 1) };
            if (p < n) b[2+p] ^= 1;
            return (a,b);
        }).ToArray();
    }

    [Benchmark(OperationsPerInvoke = Samples)]
    public int Known4() { int total = 0; foreach (var p in four) total += PrefixSites.Both4(p.A.AsSpan(1),p.B.AsSpan(2)); return total; }
    [Benchmark(OperationsPerInvoke = Samples)]
    public int Known8() { int total = 0; foreach (var p in eight) total += PrefixSites.Both8(p.A.AsSpan(1),p.B.AsSpan(2)); return total; }
    [Benchmark(OperationsPerInvoke = Samples)]
    public int Known16() { int total = 0; foreach (var p in sixteen) total += PrefixSites.Both16(p.A.AsSpan(1),p.B.AsSpan(2)); return total; }
    [Benchmark(OperationsPerInvoke = Samples)]
    public int Known4and8() { int total = 0; foreach (var p in four) total += PrefixSites.FourEight(p.A.AsSpan(1),p.B.AsSpan(2)); return total; }
}

public class Dynamic
{
    [Params("Value4", "Value8", "Value16", "Mix0to32", "First0to32")] public string Input { get; set; } = "Mix0to32";
    private (byte[] A, byte[] B, int N)[] pairs = [];
    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(71237);
        pairs = Enumerable.Range(0, Fixed.Samples).Select(_ =>
        {
            int n = Input switch { "Value4" => 4, "Value8" => 8, "Value16" => 16, _ => rng.Next(33) };
            var a = new byte[35]; var b = new byte[36]; rng.NextBytes(a);
            a.AsSpan(1,32).CopyTo(b.AsSpan(2));
            int p = Input == "First0to32" ? 0 : rng.Next(n+1);
            if (p < n) b[2+p] ^= 1;
            return (a,b,n);
        }).ToArray();
    }
    [Benchmark(OperationsPerInvoke = Fixed.Samples)]
    public int BothUnknown()
    {
        int sum = 0;
        foreach (var p in pairs) sum += PrefixSites.Unknown(p.A.AsSpan(1,p.N),p.B.AsSpan(2,p.N));
        return sum;
    }
    [Benchmark(OperationsPerInvoke = Fixed.Samples)]
    public int OneKnown()
    {
        int sum = 0;
        foreach (var p in pairs) sum += PrefixSites.FixedDynamic(p.A.AsSpan(1),p.B.AsSpan(2,p.N));
        return sum;
    }
}

public class FixedCaller
{
    private byte[] keys = [], encoded = [];
    // Model a front-coding pass over sorted fixed-width big-endian keys.
    // The savings accumulator consumes the result of each prefix call.
    [GlobalSetup]
    public void Setup()
    {
        const int n = Fixed.Samples + 1;
        var rng = new Random(54);
        keys = new byte[n*8];
        ulong current = 0;
        for (int i = 0; i < n; i++)
        {
            current += i % 8 == 0 ? 0x100000001UL : (uint)rng.Next(1, 65536);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(keys.AsSpan(i*8,8),current);
        }
        var w = new AsnWriter(AsnEncodingRules.DER);
        using (w.PushSetOf())
        {
            for (int i=1;i<=64;i++) w.WriteOctetString(new byte[]{3,5,8,13,(byte)i});
        }
        encoded=w.Encode();
    }
    [Benchmark(OperationsPerInvoke = Fixed.Samples)]
    public int FrontCode8()
    {
        int shared=0;
        for (int i=8;i<keys.Length;i+=8)
            shared+=keys.AsSpan(i-8,8).CommonPrefixLength(keys.AsSpan(i,8));
        return shared;
    }
    [Benchmark]
    public int DerShared7()
    {
        AsnDecoder.ReadSetOf(encoded,AsnEncodingRules.DER,out _,out _,out int consumed);
        return consumed;
    }
}

static class Verify
{
    static void Check(int actual, int expected)
    {
        if(actual != expected) throw new Exception($"Expected {expected}, got {actual}");
    }
    public static void Run()
    {
        var rng = new Random(123); int checks=0;
        var x = new byte[64]; var y = new byte[64];
        for (int offA=0;offA<8;offA++)
        for (int offB=0;offB<8;offB++)
        for (int n=0;n<=33;n++)
        for (int p=0;p<=33;p++)
        foreach(byte bit in new byte[]{1,128})
        {
            var a=x.AsSpan(offA,40); var b=y.AsSpan(offB,40);rng.NextBytes(a);a.CopyTo(b);b[p]^=bit;
            Check(PrefixSites.Both4(a,b),Math.Min(4,p));
            Check(PrefixSites.Both8(a,b),Math.Min(8,p));
            Check(PrefixSites.Both16(a,b),Math.Min(16,p));
            Check(PrefixSites.FourEight(a,b),Math.Min(4,p));
            Check(PrefixSites.EightFour(a,b),Math.Min(4,p));
            Check(PrefixSites.SixteenEight(a,b),Math.Min(8,p));
            Check(PrefixSites.FixedDynamic(a,b[..n]),Math.Min(Math.Min(8,n),p));
            Check(PrefixSites.DynamicFixed(a[..n],b),Math.Min(Math.Min(8,n),p));
            Check(PrefixSites.Unknown(a[..n],b[..n]),Math.Min(n,p));
            Check(PrefixSites.Both7(a,b),Math.Min(7,p));
            Check(PrefixSites.FourUShort(MemoryMarshal.Cast<byte,ushort>(a),MemoryMarshal.Cast<byte,ushort>(b)),Math.Min(8,p)/2);
            Check(PrefixSites.TwoUInt(MemoryMarshal.Cast<byte,uint>(a),MemoryMarshal.Cast<byte,uint>(b)),Math.Min(8,p)/4);
            Check(PrefixSites.OneULong(MemoryMarshal.Cast<byte,ulong>(a),MemoryMarshal.Cast<byte,ulong>(b)),Math.Min(8,p)/8);
            checks+=13;
        }
        Check(PrefixSites.FixedDynamic(x,[]),0); Check(PrefixSites.DynamicFixed([],y),0);
        Check(PrefixSites.TwoFloat(new[]{0f,float.NaN},new[]{-0f,BitConverter.Int32BitsToSingle(unchecked((int)0xffc01234))}),2);
        var model=new FixedCaller();model.Setup();Check(model.DerShared7(),452);
        if(model.FrontCode8() is <=0 or > Fixed.Samples*8)throw new Exception("Invalid front-coded prefix lengths");
        Console.WriteLine($"{checks+5} checks passed on {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
    }
    public static long Heat()
    {
        var a=new byte[64];var b=new byte[64];long total=0;
        var model=new FixedCaller();model.Setup();
        for(int i=0;i<30_000;i++)
        {
            var aa=a.AsSpan(1);var bb=b.AsSpan(2);int p=i%17;bb[p]^=1;int n=i%33;
            total+=PrefixSites.Both4(aa,bb)+PrefixSites.Both8(aa,bb)+PrefixSites.Both16(aa,bb)+PrefixSites.FourEight(aa,bb)+PrefixSites.EightFour(aa,bb)+PrefixSites.SixteenEight(aa,bb)+PrefixSites.Both7(aa,bb);
            total+=PrefixSites.FixedDynamic(aa,bb[..n])+PrefixSites.DynamicFixed(aa[..n],bb)+PrefixSites.Unknown(aa[..n],bb[..n]);
            total+=PrefixSites.FourUShort(MemoryMarshal.Cast<byte,ushort>(aa),MemoryMarshal.Cast<byte,ushort>(bb))+PrefixSites.TwoUInt(MemoryMarshal.Cast<byte,uint>(aa),MemoryMarshal.Cast<byte,uint>(bb))+PrefixSites.OneULong(MemoryMarshal.Cast<byte,ulong>(aa),MemoryMarshal.Cast<byte,ulong>(bb));
            bb[p]^=1;
            if((i%32)==0) total+=model.FrontCode8();
        }
        return total+model.DerShared7();
    }
}
