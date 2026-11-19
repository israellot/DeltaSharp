using BenchmarkDotNet.Attributes;
using DeltaSharp.Util.Hashtable;
using System;

namespace SharpFossilBenchmarks.Benchmarks;

// Each operation includes construction, one lookup and disposal. Setup warms
// the pools so these benchmarks measure reuse, as in repeated delta creation.
[MemoryDiagnoser]
[AllStatisticsColumn]
[HideColumns("Median", "StdError", "Q1", "Q3", "Max", "Min")]
public class HashtableConstructionAddressSize
{
    private byte[] _data;

    // Zero is the empty-slot sentinel, so byte indices support 255 blocks.
    [Params(512, 1024, byte.MaxValue * 8)]
    public int Bytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Bytes];
        new Random(11).NextBytes(_data);
        Hashtable8();
        Hashtable16();
#if DELTASHARP_HASHTABLE24
        Hashtable24();
#endif
        Hashtable32();
    }

    [Benchmark]
    public int Hashtable8()
    {
        using var hashtable = new Hashtable64_8(_data);
        return hashtable.Match(_data);
    }

    [Benchmark]
    public int Hashtable16()
    {
        using var hashtable = new Hashtable64_16(_data);
        return hashtable.Match(_data);
    }

#if DELTASHARP_HASHTABLE24
    [Benchmark]
    public int Hashtable24()
    {
        using var hashtable = new Hashtable64_24(_data);
        return hashtable.Match(_data);
    }
#endif

    [Benchmark(Baseline = true)]
    public int Hashtable32()
    {
        using var hashtable = new Hashtable64_32(_data);
        return hashtable.Match(_data);
    }
}

[MemoryDiagnoser]
[AllStatisticsColumn]
[HideColumns("Median", "StdError", "Q1", "Q3", "Max", "Min")]
public class HashtableConstructionThroughput
{
    private byte[] _data;

    [Params(512 * 1024, 1024 * 1024, 10 * 1024 * 1024, 64 * 1024 * 1024)]
    public int Bytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Bytes];
        new Random(11).NextBytes(_data);
#if DELTASHARP_HASHTABLE24
        Hashtable24();
#endif
        Hashtable32();
    }

    [Benchmark(Baseline = true)]
    public int Hashtable32()
    {
        using var hashtable = new Hashtable64_32(_data);
        return hashtable.Match(_data);
    }

#if DELTASHARP_HASHTABLE24
    [Benchmark]
    public int Hashtable24()
    {
        using var hashtable = new Hashtable64_24(_data);
        return hashtable.Match(_data);
    }
#endif
}
