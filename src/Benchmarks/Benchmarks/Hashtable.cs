using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using DeltaSharp.Util.Hashtable;
using System;

namespace SharpFossilBenchmarks.Benchmarks;



[MemoryDiagnoser(false)]
[AllStatisticsColumn]
[HideColumns("Median","StdError","Q1","Q3","Max","Min")]
public class HashtableConstructionAddressSize
{

    public ReadOnlyMemory<byte> _1Mb { get; set; }

    public ReadOnlyMemory<byte> _512k { get; set; }

    public ReadOnlyMemory<byte> _2k { get; set; }

    public HashtableConstructionAddressSize()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[1024*1024];
        var random = new Random(11);
        random.NextBytes(data);

        _1Mb = data;
        _512k = new ReadOnlyMemory<byte>(data,0, 1024 * 512);
        _2k = new ReadOnlyMemory<byte>(data, 0, 1024*2);
    }
        

    [Benchmark]
    public int Hashtable8()
    {
        var hashtable = new Hashtable64_8(_2k.Span);

        return hashtable.Match(_2k.Span);
    }

    [Benchmark]
    public int Hashtable16()
    {
        var hashtable = new Hashtable64_16(_2k.Span);

        return hashtable.Match(_2k.Span);
    }


    [Benchmark]
    public int Hashtable24()
    {
        var hashtable = new Hashtable64_24(_2k.Span);

        return hashtable.Match(_2k.Span);
    }
        
    [Benchmark]
    public int Hashtable32()
    {
        var hashtable = new Hashtable64_32(_2k.Span);

        return hashtable.Match(_2k.Span);
    }

}



[MemoryDiagnoser(true)]
[AllStatisticsColumn]
[HideColumns("Median", "StdError", "Q1", "Q3", "Max", "Min")]
public class HashtableConstructionThroughtput
{

    public ReadOnlyMemory<byte> _10Mb { get; set; }

    public HashtableConstructionThroughtput()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[1024 * 1024*10];
        var random = new Random(11);
        random.NextBytes(data);

        _10Mb = data;
    }

    [Benchmark]
    public int Hashtable32()
    {
        var hashtable = new Hashtable64_32(_10Mb.Span);

        return hashtable.Match(_10Mb.Span);
    }

    [Benchmark]
    public int Hashtable24()
    {
        var hashtable = new Hashtable64_24(_10Mb.Span);

        return hashtable.Match(_10Mb.Span);
    }

}