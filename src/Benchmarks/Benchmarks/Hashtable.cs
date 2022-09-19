using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DeltaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        var hashtable = new DeltaSharp.Hashtable64_8(_2k.Span);

        return hashtable.Match(_2k.Span);
    }

    [Benchmark]
    public int Hashtable16()
    {
        var hashtable = new DeltaSharp.Hashtable64_16(_2k.Span);

        return hashtable.Match(_2k.Span);
    }


    [Benchmark]
    public int Hashtable24()
    {
        var hashtable = new DeltaSharp.Hashtable64_24(_2k.Span);

        return hashtable.Match(_2k.Span);
    }
        
    [Benchmark]
    public int Hashtable32()
    {
        var hashtable = new DeltaSharp.Hashtable64_32(_2k.Span);

        return hashtable.Match(_2k.Span);
    }

}



[MemoryDiagnoser(false)]
[AllStatisticsColumn]
[HideColumns("Median", "StdError", "Q1", "Q3", "Max", "Min")]
public class HashtableConstructionThroughtput
{

    public ReadOnlyMemory<byte> _1Mb { get; set; }

    public HashtableConstructionThroughtput()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[1024 * 1024];
        var random = new Random(11);
        random.NextBytes(data);

        _1Mb = data;
    }

    [Benchmark]
    public int Hashtable32()
    {
        var hashtable = new DeltaSharp.Hashtable64_32(_1Mb.Span);

        return hashtable.Match(_1Mb.Span);
    }

    [Benchmark]
    public int Hashtable24()
    {
        var hashtable = new DeltaSharp.Hashtable64_24(_1Mb.Span);

        return hashtable.Match(_1Mb.Span);
    }

}