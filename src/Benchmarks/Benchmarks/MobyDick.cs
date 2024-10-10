using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DeltaSharp;
using DeltaSharp.Format;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SharpFossilBenchmarks.Benchmarks;

[DisassemblyDiagnoser]
[AllStatisticsColumn]
[MemoryDiagnoser]
[HideColumns("Median","StdError","Q1","Q3","Max","Min")]
public class MobyDick
{
    public byte[] Source;
    public byte[] Target;

    IDeltaCreate _writerBinary;
    IDeltaApply _readerBinary;

    public MobyDick()
    {

    }

    

    [GlobalSetup]
    public void Setup()
    {
        _writerBinary = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>();
        _readerBinary = new DeltaApply<BinaryDeltaReader, DeltaChecksum>();

        Source = File.ReadAllBytes("Data/pg15.txt");
        Target = File.ReadAllBytes("Data/pg2701.txt");
    }


    [Benchmark]
    public ulong DeltaCreate()
    {
        
        var delta = _writerBinary.Create(Source, Target);


        return delta.Crc32;
    }

    [Benchmark]
    public ulong DeltaRoundtrip()
    {
        var delta = _writerBinary.Create(Source, Target);

        File.WriteAllBytes("Data/delta.txt", delta.Data.ToArray());

        var result = _readerBinary.Apply(Source, delta.Data.Span);

#if DEBUG
        var equal = result.Data.Span.SequenceEqual(Target);
#endif

        return delta.Crc32;
    }

}

