using BenchmarkDotNet.Attributes;
using DeltaSharp;
using System;
using System.IO;
using Delta = DeltaSharp.DeltaSharp;

namespace SharpFossilBenchmarks.Benchmarks;

[MemoryDiagnoser]
public class PatchOperations
{
    [Params("MobyDick", "SparseEdits", "Unrelated")]
    public string Workload { get; set; }

    [Params("Binary", "Fossil", "ASCII")]
    public string Format { get; set; }

    private byte[] _source;
    private byte[] _target;
    private IDelta _codec;
    private ReadOnlyMemory<byte> _patch;
    private byte[] _destination;

    [GlobalSetup]
    public void Setup()
    {
        _codec = Format switch
        {
            "Binary" => Delta.Binary,
            "Fossil" => Delta.Fossil,
            "ASCII" => Delta.ASCII,
            _ => throw new ArgumentOutOfRangeException(nameof(Format))
        };

        if (Workload == "MobyDick")
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "Data");
            _source = File.ReadAllBytes(Path.Combine(directory, "pg15.txt"));
            _target = File.ReadAllBytes(Path.Combine(directory, "pg2701.txt"));
        }
        else
        {
            var random = new Random(71237);
            _source = new byte[1024 * 1024];
            random.NextBytes(_source);
            _target = new byte[_source.Length];
            switch (Workload)
            {
                case "SparseEdits":
                    _source.CopyTo(_target, 0);
                    for (var i = 0; i < _target.Length; i += 4096)
                        _target[i] ^= 0xff;
                    break;
                case "Unrelated":
                    random.NextBytes(_target);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Workload));
            }
        }

        _patch = Create().Data;
        _destination = new byte[_target.Length];
        if (!Apply().Data.Span.SequenceEqual(_target) || !ApplyInto().Data.Span.SequenceEqual(_target))
            throw new InvalidOperationException($"{Workload} / {Format} failed to reconstruct the target.");
        Console.WriteLine($"{Workload} / {Format}: source={_source.Length} target={_target.Length} patch={_patch.Length} bytes");
    }

    [Benchmark]
    public DeltaResult Create() => _codec.Create.Create(_source, _target);

    [Benchmark]
    public DeltaResult Apply() => _codec.Apply.Apply(_source, _patch.Span);

    [Benchmark]
    public DeltaResult ApplyInto() => _codec.Apply.Apply(_source, _patch.Span, _destination);
}
