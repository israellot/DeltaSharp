using BenchmarkDotNet.Attributes;
using DeltaSharp;
using System;
using Delta = DeltaSharp.DeltaSharp;

namespace SharpFossilBenchmarks.Benchmarks;

[MemoryDiagnoser]
public class DeltaSearch
{
    private byte[] _source;
    private byte[] _target;

    [Params(8192, 32768, 65536)]
    public int Bytes { get; set; }

    [Params("RepetitiveChanges", "LateMatch")]
    public string Workload { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _target = new byte[Bytes];
        if (Workload == "LateMatch")
        {
            // The full match is candidate 257, just beyond bounded mode's budget.
            _source = new byte[256 * 16 + Bytes];
            for (var i = 0; i < 256; i++)
                _source.AsSpan(i * 16 + 8, 8).Fill(1);
        }
        else
        {
            _source = new byte[Bytes];
            for (var i = 8; i < Bytes; i += 9)
                _target[i] = 1;
        }

        Console.WriteLine($"Patch bytes: exhaustive={Exhaustive()}, bounded={Bounded()}");
    }

    [Benchmark(Baseline = true)]
    public int Exhaustive() => Delta.CreateBinary(_source, _target, DeltaSearchMode.Exhaustive).Data.Length;

    [Benchmark]
    public int Bounded() => Delta.CreateBinary(_source, _target, DeltaSearchMode.Bounded).Data.Length;
}
