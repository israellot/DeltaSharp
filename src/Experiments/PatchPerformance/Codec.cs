// Compiled into each isolated copy of DeltaSharp. The benchmark binds delegates once during setup.
using DeltaSharp;
using Delta = DeltaSharp.DeltaSharp;

public static class Codec
{
    public static ReadOnlyMemory<byte> Create(byte[] source, byte[] target) =>
        Delta.CreateBinary(source, target).Data;

    public static ReadOnlyMemory<byte> Apply(byte[] source, ReadOnlyMemory<byte> patch) =>
        Delta.Binary.Apply.Apply(source, patch.Span).Data;

    public static ReadOnlyMemory<byte> Reuse(byte[] source, ReadOnlyMemory<byte> patch, byte[] output) =>
        Delta.Binary.Apply.Apply(source, patch.Span, output).Data;

    public static byte[] Verify(byte[] source, byte[] target, string format, bool bounded)
    {
        IDelta codec = format switch
        {
            "Binary" => new DeltaBinary(bounded ? DeltaSearchMode.Bounded : DeltaSearchMode.Exhaustive),
            "Fossil" => new DeltaFossil(bounded ? DeltaSearchMode.Bounded : DeltaSearchMode.Exhaustive),
            "ASCII" => new DeltaASCII(bounded ? DeltaSearchMode.Bounded : DeltaSearchMode.Exhaustive),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var patch = codec.Create.Create(source, target).Data;
        var output = new byte[target.Length];
        Array.Fill(output, (byte)0xcc);
        if (!codec.Apply.Apply(source, patch.Span).Data.Span.SequenceEqual(target) ||
            !codec.Apply.Apply(source, patch.Span, output).Data.Span.SequenceEqual(target) ||
            !output.AsSpan().SequenceEqual(target))
            throw new InvalidOperationException("Round-trip failure");
        return patch.ToArray();
    }
}
