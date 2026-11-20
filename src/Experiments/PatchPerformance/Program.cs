using System.Reflection;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (args.SequenceEqual(new[] { "--verify" }))
{
    Cases.Verify();
    return 0;
}

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Creation).Assembly).Run(args).ToArray();
return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors &&
    s.Reports.Any() && s.Reports.All(r => r.Success)) ? 0 : 1;

public static class Cases
{
    public static string[] Variants => Environment.Version.Major >= 9
        ? ["Current", "Cap4K", "Cap16K", "Cap64K", "Cap4KTrailer", "Prefix", "Generic", "Split"]
        : ["Current", "Prefix", "Split"];

    public static readonly string[] Names =
        ["Random1K", "Random32K", "Records32K", "Sparse1M", "Dense1M", "MobyDick", "Random1M"];

    public static T Bind<T>(string variant, string method) where T : Delegate
    {
        var directory = Environment.GetEnvironmentVariable("DELTA_TRIAL_DLLS")!;
        var assembly = Assembly.LoadFrom(Path.Combine(directory, variant + ".dll"));
        return assembly.GetType("Codec")!.GetMethod(method)!.CreateDelegate<T>();
    }

    public static (byte[] Source, byte[] Target) Inputs(string name)
    {
        if (name == "MobyDick")
        {
            var data = Environment.GetEnvironmentVariable("DELTA_TRIAL_DATA")!;
            return (File.ReadAllBytes(Path.Combine(data, "pg15.txt")), File.ReadAllBytes(Path.Combine(data, "pg2701.txt")));
        }
        int length = name switch
        {
            "Random1K" => 1024,
            "Random32K" or "Records32K" => 32768,
            "Sparse1M" or "Dense1M" or "Random1M" => 1048576,
            _ => throw new ArgumentException(name)
        };
        var rng = new Random(71237);
        var source = new byte[length];
        rng.NextBytes(source);
        var target = (byte[])source.Clone();
        if (name.StartsWith("Random", StringComparison.Ordinal))
            rng.NextBytes(target);
        else if (name == "Records32K")
        {
            var record = new byte[64];
            rng.NextBytes(record);
            for (int i = 0; i < length; i += 64)
            {
                record.CopyTo(source, i);
                record.CopyTo(target, i);
                source[i + 40] ^= (byte)(i / 64);
                target[i + 41] ^= (byte)(i / 64);
            }
        }
        else
        {
            int stride = name == "Sparse1M" ? 4096 : 4;
            for (int i = 0; i < length; i += stride)
                target[i] ^= 255;
        }
        return (source, target);
    }

    public static void Verify()
    {
        int comparisons = 0;
        var inputs = Names.Select(Inputs).ToList();
        var rng = new Random(11);
        foreach (int n in new[] { 0, 1, 7, 8, 15, 16, 17, 31, 63, 255, 2047, 2048, 524279, 524280 })
        {
            var source = new byte[n];
            rng.NextBytes(source);
            var target = (byte[])source.Clone();
            if (n > 0) target[n / 2] ^= 255;
            inputs.Add((source, target));
            inputs.Add((source, [17, .. target, 42]));
        }
        // Exercise large literals followed by a COPY and by the final checksum.
        foreach (int literalSize in new[] { 4095, 4096, 4097, 65536 })
        {
            var source = new byte[32768];
            var literal = new byte[literalSize];
            rng.NextBytes(source);
            rng.NextBytes(literal);
            inputs.Add((source, [.. literal, .. source, .. literal]));
        }
        var baseline = Bind<Func<byte[], byte[], string, bool, byte[]>>("Current", "Verify");
        foreach (var variant in Variants.Where(v => v != "Current"))
        {
            var candidate = Bind<Func<byte[], byte[], string, bool, byte[]>>(variant, "Verify");
            foreach (var (source, target) in inputs)
                foreach (var format in new[] { "Binary", "Fossil", "ASCII" })
                    foreach (var bounded in new[] { false, true })
                    {
                        if (!baseline(source, target, format, bounded).AsSpan().SequenceEqual(candidate(source, target, format, bounded)))
                            throw new InvalidOperationException($"Changed patch: {variant} {format} {bounded}");
                        comparisons++;
                    }
        }
        Console.WriteLine($"{comparisons} differential comparisons passed on {Environment.Version}");
        foreach (var name in Names)
            foreach (var variant in Variants)
            {
                var (source, target) = Inputs(name);
                var patch = Bind<Func<byte[], byte[], ReadOnlyMemory<byte>>>(variant, "Create")(source, target);
                if (!MemoryMarshal.TryGetArray(patch, out var buffer)) throw new InvalidOperationException();
                Console.WriteLine($"Capacity,{name},{variant},{patch.Length},{buffer.Array!.Length}");
            }
    }
}

[MemoryDiagnoser]
public class Creation
{
    public IEnumerable<string> Variants => Cases.Variants;
    public IEnumerable<string> Inputs => Cases.Names;
    [ParamsSource(nameof(Variants))] public string Variant { get; set; } = "";
    [ParamsSource(nameof(Inputs))] public string Input { get; set; } = "";
    private byte[] source = null!, target = null!;
    private Func<byte[], byte[], ReadOnlyMemory<byte>> create = null!;

    [GlobalSetup]
    public void Setup()
    {
        (source, target) = Cases.Inputs(Input);
        create = Cases.Bind<Func<byte[], byte[], ReadOnlyMemory<byte>>>(Variant, "Create");
    }

    [Benchmark]
    public ReadOnlyMemory<byte> Create() => create(source, target);
}

[MemoryDiagnoser]
public class Application
{
    [Params("Random1K", "Records32K", "Sparse1M", "MobyDick", "Random1M")]
    public string Input { get; set; } = "";
    private byte[] source = null!, output = null!;
    private ReadOnlyMemory<byte> patch;
    private Func<byte[], ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> apply = null!;
    private Func<byte[], ReadOnlyMemory<byte>, byte[], ReadOnlyMemory<byte>> reuse = null!;

    [GlobalSetup]
    public void Setup()
    {
        (source, var target) = Cases.Inputs(Input);
        patch = Cases.Bind<Func<byte[], byte[], ReadOnlyMemory<byte>>>("Current", "Create")(source, target);
        output = new byte[target.Length];
        apply = Cases.Bind<Func<byte[], ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>("Current", "Apply");
        reuse = Cases.Bind<Func<byte[], ReadOnlyMemory<byte>, byte[], ReadOnlyMemory<byte>>>("Current", "Reuse");
        if (!apply(source, patch).Span.SequenceEqual(target) || !reuse(source, patch, output).Span.SequenceEqual(target))
            throw new InvalidOperationException();
    }

    [Benchmark(Baseline = true)] public ReadOnlyMemory<byte> Allocate() => apply(source, patch);
    [Benchmark] public ReadOnlyMemory<byte> Reuse() => reuse(source, patch, output);
}
