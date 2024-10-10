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
using DeltaSharp.Util.Hashtable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpFossilBenchmarks.Benchmarks;


[MemoryDiagnoser(false)]
[DisassemblyDiagnoser]
[AllStatisticsColumn]
[HideColumns("Median","StdError","Q1","Q3","Max","Min")]
public class HashingBenchmark
{
    
    public ulong Source64 { get; set; }

    public HashingBenchmark()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[32];
        Random.Shared.NextBytes(data);
        Source64 = (ulong)MemoryMarshal.Cast<byte, ulong>(data)[0];
    }

    [Benchmark]
    public ulong SplitableRandom()
    {
        return Hash.Hash64(Source64);
    }

}


