using System.Formats.Asn1;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (args.SequenceEqual(new[] { "--verify" }))
{
    int checks = 0;
    foreach (int n in Enumerable.Range(0, 34).Append(65))
    foreach (int padA in Enumerable.Range(0, 8))
    foreach (int padB in Enumerable.Range(0, 8))
    foreach (int extra in new[] { -1, 0, 1 })
    {
        int lenA = n + Math.Max(0, extra), lenB = n - Math.Min(0, extra);
        var a = new byte[padA + lenA];
        var b = new byte[padB + lenB];
        for (int i = 0; i < n; i++) a[padA + i] = b[padB + i] = (byte)(17 + i * 29);
        for (int p = 0; p <= n; p++)
        foreach (byte bit in new byte[] { 1, 128 })
        {
            if (p < n) b[padB + p] ^= bit;
            ReadOnlySpan<byte> x = a.AsSpan(padA), y = b.AsSpan(padB);
            Check(x, y, p);
            Check(MemoryMarshal.Cast<byte, ushort>(x), MemoryMarshal.Cast<byte, ushort>(y), p / 2);
            Check(MemoryMarshal.Cast<byte, uint>(x), MemoryMarshal.Cast<byte, uint>(y), p / 4);
            checks += 3;
            if (p < n) b[padB + p] ^= bit;
        }
    }
    var der = new DerSet();
    der.Setup();
    if (der.Validate() != 452) throw new Exception("Unexpected encoded size");
    Console.WriteLine($"{checks} byte/ushort/uint prefix checks passed on {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Prefix).Assembly).Run(args).ToArray();
return summaries.Length != 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.Any() && s.Reports.All(r => r.Success)) ? 0 : 1;

static void Check<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, int expected) where T : IEquatable<T>
{
    if (a.CommonPrefixLength(b) != expected) throw new Exception($"Incorrect {typeof(T)} prefix (expected {expected})");
}

public class Prefix
{
    [Params("0-equal", "1-equal", "3-last", "4-first", "4-last", "5-last", "6-last", "7-first", "7-last", "7-equal", "vary4to7",
        "4-mixed", "4-mostlyFirst", "4-equal", "5-at1", "8-first", "8-at4", "12-first", "12-at8",
        "vary0to32", "vary4to7-first", "vary0to32-first", "8-last", "15-last", "16-last", "64-equal", "1024-equal")]
    public string Input { get; set; } = "";

    private const int Samples = 256;
    private (byte[] A, byte[] B)[] pairs = [];
    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(71237);
        pairs = new (byte[], byte[])[Samples];
        for (int i = 0; i < Samples; i++)
        {
            int length = Input switch
            {
                "vary4to7" or "vary4to7-first" => rng.Next(4, 8),
                "vary0to32" or "vary0to32-first" => rng.Next(33),
                _ => int.Parse(Input.Split('-')[0])
            };
            var a = new byte[length + 1];
            var b = new byte[length + 2];
            rng.NextBytes(a);
            a.AsSpan(1).CopyTo(b.AsSpan(2));
            int p = Input.EndsWith("-first", StringComparison.Ordinal) ? 0 :
                Input.StartsWith("vary", StringComparison.Ordinal) ? rng.Next(length + 1) :
                Input.EndsWith("-last", StringComparison.Ordinal) ? length - 1 : length;
            p = Input switch
            {
                "4-mixed" => rng.Next(5),
                "4-mostlyFirst" => rng.Next(20) switch { 0 => 3, 1 => 4, _ => 0 },
                "5-at1" => 1,
                "8-at4" => 4,
                "12-at8" => 8,
                _ => p
            };
            if (p < length) b[p+2] ^= 1;
            if (a.AsSpan(1).CommonPrefixLength(b.AsSpan(2)) != p) throw new Exception();
            pairs[i] = (a, b);
        }
    }
    [Benchmark(OperationsPerInvoke = Samples)]
    public int Compare()
    {
        int sum = 0;
        foreach (var pair in pairs)
            sum += pair.A.AsSpan(1).CommonPrefixLength(pair.B.AsSpan(2));
        return sum;
    }
}

public class DerSet
{
    [Params("Shared7", "Mixed")] public string Input { get; set; } = "Shared7";
    private byte[] encoded = [];
    [GlobalSetup]
    public void Setup()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        {
            var rng = new Random(8);
            for (int i = 1; i <= 64; i++)
            {
                var value = new byte[] { 3, 5, 8, 13, (byte)i };
                if (Input == "Mixed")
                {
                    value = new byte[i % 18];
                    rng.NextBytes(value);
                }
                writer.WriteOctetString(value);
            }
        }
        encoded = writer.Encode();
    }

    [Benchmark]
    public int Validate()
    {
        AsnDecoder.ReadSetOf(encoded, AsnEncodingRules.DER, out _, out _, out int consumed);
        return consumed;
    }
}
