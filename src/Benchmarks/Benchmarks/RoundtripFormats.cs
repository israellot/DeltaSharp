using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Runtime.Utilities;
using DeltaSharp;
using DeltaSharp.Format;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpFossilBenchmarks.Benchmarks;


[DisassemblyDiagnoser]
[AllStatisticsColumn]
[MemoryDiagnoser]
[HideColumns("Median","StdError","Q1","Q3","Max","Min")]
public class RoundtripFormats
{
    public byte[] Source;
    public byte[] Target;
    public int Lines => Bytes / 16;

    [Params(1024,1024*8,1024*32, 1024 * 128,1024 * 1024)]
    public int Bytes { get; set; }

    IDeltaCreate _writerBinary;
    IDeltaApply _readerBinary;

    IDeltaCreate _writerFossil;
    IDeltaApply _readerFossil;

    public RoundtripFormats()
    {

    }

    [GlobalSetup]
    public void Setup()
    {
        _writerFossil = new DeltaCreate<FossilDeltaWriter, DeltaChecksum>();
        _readerFossil = new DeltaApply<FossilDeltaReader, DeltaChecksum>();
        _writerBinary = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>();
        _readerBinary = new DeltaApply<BinaryDeltaReader, DeltaChecksum>();

        var random = new Random(11);
        var generator = new TargetGenerator(random: random);

        generator.Execute($"ADD 0 RND(15) {Lines}");

        Source = generator.Source;

        for(var i = 0; i < Lines / 10; i++)
        {
            var setLine = random.Next(0, Lines);

            generator.Execute($"SET {setLine} RND(15)");
        }

        Target = generator.Source;

    }

    [Benchmark(Baseline =true)]
    public ulong Fossil()
    {
        var delta = _writerFossil.Create(Source, Target);

        var result = _readerFossil.Apply(Source, delta.Data.Span);

#if DEBUG
        var equal = result.Data.Span.SequenceEqual(Target);
#endif

        return delta.Crc32;
    }

    [Benchmark]
    public ulong Binary()
    {
        
        var delta = _writerBinary.Create(Source, Target);

        var result = _readerBinary.Apply(Source, delta.Data.Span);

#if DEBUG
        var equal = result.Span.SequenceEqual(Target);
#endif

        return delta.Crc32;
    }

}


