using DeltaSharp;
using DeltaSharp.Format;

namespace UnitTests;

public class DeltaRoundTripTests
{
    public static IEnumerable<object[]> BoundaryCases()
    {
        int[] lengths = [0, 1, 7, 8, 9, 15, 16, 17, 31, 32, 33, 255, 256, 2047, 2048];
        foreach (var format in new[] { "Binary", "ASCII", "Fossil" })
            foreach (var length in lengths)
                yield return [format, length];
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void PublicApiRoundTripsBoundarySizesAndBinaryPayloads(string format, int length)
    {
        var random = new Random(9127 + length);
        var source = new byte[length];
        random.NextBytes(source);
        var unrelated = new byte[length + 17];
        random.NextBytes(unrelated);
        byte[][] targets = [[], source, source.Reverse().ToArray(), unrelated,
            source.Concat(new byte[] { 0, 255, 13, 10, (byte)'v', (byte)';' }).ToArray()];
        foreach (var target in targets)
            AssertRoundTrip(format, source, target);
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void SeededEditsRoundTrip(string format)
    {
        var random = new Random(20261119);
        for (int sample = 0; sample < 150; sample++)
        {
            var source = new byte[random.Next(0, 4097)];
            random.NextBytes(source);
            if (sample % 3 == 0)
                Array.Fill(source, (byte)(sample % 7));
            var target = source.ToList();
            for (int edit = 0; edit < 8; edit++)
            {
                int position = random.Next(target.Count + 1);
                int count = Math.Min(random.Next(1, 33), target.Count - position);
                if (edit % 2 == 0)
                    target.RemoveRange(position, count);
                else
                {
                    var insert = new byte[random.Next(1, 33)];
                    random.NextBytes(insert);
                    target.InsertRange(position, insert);
                }
            }
            AssertRoundTrip(format, source, target.ToArray());
        }
    }

    private static void AssertRoundTrip(string format, byte[] source, byte[] target)
    {
        // Exercise the static convenience API as well as caller-owned memory.
        var patch = format switch
        {
            "Binary" => DeltaSharp.DeltaSharp.CreateBinary(source, target),
            "ASCII" => DeltaSharp.DeltaSharp.CreateASCII(source, target),
            _ => DeltaSharp.DeltaSharp.CreateFossil(source, target)
        };
        var restored = format switch
        {
            "Binary" => DeltaSharp.DeltaSharp.ApplyBinary(source, patch.Data.Span),
            "ASCII" => DeltaSharp.DeltaSharp.ApplyASCII(source, patch.Data.Span),
            _ => DeltaSharp.DeltaSharp.ApplyFossil(source, patch.Data.Span)
        };
        Assert.Equal(target, restored.Data.ToArray());
        Assert.Equal(patch.Crc32, restored.Crc32);

        var apply = AuditRegressionTests.Format(format).Apply;
        Assert.Equal((ulong)target.Length, apply.GetOutputLength(patch.Data.Span));
        var buffer = Enumerable.Repeat((byte)0xa5, target.Length + 10).ToArray();
        var result = apply.Apply(source, patch.Data.Span, buffer.AsMemory(3, target.Length + 4));
        Assert.Equal(target, result.Data.ToArray());
        Assert.Equal(patch.Crc32, result.Crc32);
        Assert.All(buffer.Take(3).Concat(buffer.Skip(target.Length + 3)), b => Assert.Equal(0xa5, b));
        if (target.Length != 0)
        {
            buffer[3] ^= 0xff;
            Assert.Equal(buffer[3], result.Data.Span[0]);
        }
    }
}
