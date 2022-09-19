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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpFossilBenchmarks.Benchmarks;


[DisassemblyDiagnoser]
[AllStatisticsColumn]
[HideColumns("Median","StdError","Q1","Q3","Max","Min")]
public class HashingBenchmark
{
    public byte[] Source256;
    public byte[] Source128;
    public ulong Source64 { get; set; }

    public uint Source32 { get; set; }

    public HashingBenchmark()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[32];
        Random.Shared.NextBytes(data);

        Source256 = data;
        Source128 = data.Take(16).ToArray();
        Source64 = (ulong)MemoryMarshal.Cast<byte, ulong>(data)[0];
        Source32 = (uint)MemoryMarshal.Cast<byte, uint>(data)[0];
    }

    [Benchmark]
    public ulong SplitableRandom()
    {
        ulong a = 0;
        for (var i = 0; i < 4; i++)
            a += Hash.Hash64(Source64+(ulong)i);

        return a;
    }

    [Benchmark]
    public uint Lowbias32()
    {
        uint a = 0;
        for (uint i = 0; i < 8; i++)
            a += Hash.Hash32(Source32+i);

        return a;
    }


    [Benchmark]
    public uint MurmurHash3_16Bytes()
    {
        return Hash.Hash32(Source128) + Hash.Hash32(Source128); ;
    }

    [Benchmark]
    public uint MurmurHash3_32Bytes()
    {       
        return Hash.Hash32(Source256);
    }

    


}


