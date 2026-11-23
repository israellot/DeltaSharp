using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (args is ["--verify"] or ["--jit"])
{
    foreach (var name in Pages.Cases)
    {
        var b = new Pages { Input = name };
        b.Setup();
        if (args[0] == "--jit")
        {
            long acc = 0;
            for (int j = 0; j < 5; j++)
            {
                for (int i = 0; i < 2400; i++)
                {
                    acc += b.FirstDifference();
                    acc += b.JointRunBytes();
                }
                Thread.Sleep(25);
            }
            Console.WriteLine($"{name} checksum {acc}");
        }
    }
    Console.WriteLine("All scenario results match independent expected offsets and encoded byte counts.");
    return 0;
}
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Pages).Assembly).Run(args).ToArray();
return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.Any() && s.Reports.All(r => r.Success)) ? 0 : 1;

public class Pages
{
    public static IEnumerable<string> Cases => ["Header0","Header7","LastPayloadByte","EqualPages"];
    [ParamsSource(nameof(Cases))] public string Input { get; set; } = "";
    private byte[] a = [], b = [];

    [GlobalSetup]
    public void Setup()
    {
        a = new byte[Page.Size];
        var random = new Random(17);
        random.NextBytes(a);
        b = (byte[])a.Clone();

        // Page checksums/LSNs are excluded. All cases differ in this masked region.
        b[8] ^= 1; b[16] ^= 1;
        int p = Input switch
        {
            "Header0" => 0,
            "Header7" => 7,
            "LastPayloadByte" => Page.Size - 1,
            "EqualPages" => -1,
            _ => throw new ArgumentOutOfRangeException(nameof(Input))
        };
        if (p >= 0) b[p] ^= 0x80;
        if (FirstDifference() != p || JointRunBytes() != (p < 0 ? 0 : 5))
            throw new Exception("Incorrect page result: " + Input);
    }
    [Benchmark] public int FirstDifference() => PageImageCodec.FirstDifference(a, b);
    [Benchmark] public int JointRunBytes() => SqlSchemaProvisioner.Measure(a, b);
}
