using DeltaSharp;
using DeltaSharp.Format;
using static DeltaSharp.DeltaApplyException;

namespace UnitTests;

public class DeltaValidationTests
{
    public static IEnumerable<object[]> InvalidCommands()
    {
        foreach (var format in new[] { "Binary", "ASCII", "Fossil" })
        {
            yield return [format, "copy-position", DeltaApplyExceptionError.InvalidCopyPosition];
            yield return [format, "copy-source-length", DeltaApplyExceptionError.InvalidCopyLength];
            yield return [format, "copy-output-length", DeltaApplyExceptionError.InvalidCopyLength];
            yield return [format, "insert-output-length", DeltaApplyExceptionError.InvalidInsertLength];
            yield return [format, "checksum", DeltaApplyExceptionError.InvalidChecksum];
            yield return [format, "incomplete", DeltaApplyExceptionError.IncompleteDelta];
            yield return [format, "oversize", DeltaApplyExceptionError.InvalidOutputLength];
            yield return [format, "missing-header", DeltaApplyExceptionError.InvalidDelta];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public void InvalidPatchesHaveSpecificErrors(string format, string scenario, DeltaApplyExceptionError expected)
    {
        using var writer = AuditRegressionTests.Writer(format);
        if (scenario != "missing-header")
            writer.WriteLength(scenario == "oversize" ? (ulong)int.MaxValue + 1 : 4);
        switch (scenario)
        {
            case "copy-position": writer.WriteCopy(format != "Fossil" ? ulong.MaxValue : uint.MaxValue, 1); break;
            case "copy-source-length": writer.WriteCopy(3, 4); break;
            case "copy-output-length": writer.WriteCopy(0, 5); break;
            case "insert-output-length": writer.WriteInsert(new byte[5]); break;
            case "checksum": writer.WriteInsert(new byte[4]); writer.WriteChecksum(1); break;
            case "missing-header": writer.WriteChecksum(0); break;
        }
        var patch = writer.GetOutput().ToArray();
        var apply = AuditRegressionTests.Format(format).Apply;
        Assert.Equal(expected, Assert.Throws<DeltaApplyException>(() => apply.Apply(new byte[4], patch)).Error);
        Assert.Equal(expected, Assert.Throws<DeltaApplyException>(() => apply.Apply(new byte[4], patch, new byte[4])).Error);
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void TooSmallDestinationIsRejectedBeforeWriting(string format)
    {
        var codec = AuditRegressionTests.Format(format);
        var patch = codec.Create.Create([], new byte[4]);
        var output = new byte[] { 42, 42, 42 };
        var ex = Assert.Throws<DeltaApplyException>(() => codec.Apply.Apply([], patch.Data.Span, output));
        Assert.Equal(DeltaApplyExceptionError.OutputMemorySizeLessThanRequired, ex.Error);
        Assert.Equal(new byte[] { 42, 42, 42 }, output);
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void EveryTruncatedPatchPrefixFailsWithATypedError(string format)
    {
        var codec = AuditRegressionTests.Format(format);
        var patch = codec.Create.Create([], new byte[] { 0, 255, 13, 10, 32, 59, 64 }).Data.ToArray();
        for (int length = 0; length < patch.Length; length++)
        {
            int prefixLength = length;
            var ex = Record.Exception(() => codec.Apply.Apply([], patch.AsSpan(0, prefixLength), new byte[7]));
            Assert.True(ex is DeltaReaderException or DeltaApplyException, $"{format} prefix {length}: {ex}");
        }
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void SeededMalformedInputsNeverEscapeWithRuntimeBoundsErrors(string format)
    {
        var apply = AuditRegressionTests.Format(format).Apply;
        var random = new Random(173);
        var source = new byte[64];
        for (int sample = 0; sample < 2000; sample++)
        {
            using var writer = AuditRegressionTests.Writer(format);
            writer.WriteLength(64);
            var tail = new byte[random.Next(0, 65)];
            random.NextBytes(tail);
            var patch = writer.GetOutput().ToArray().Concat(tail).ToArray();
            var ex = Record.Exception(() => apply.Apply(source, patch, new byte[64]));
            Assert.True(ex is null or DeltaReaderException or DeltaApplyException, $"{format} sample {sample}: {ex}");
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void CustomReaderMustConsumeBytesInsideThePatch(uint consumed)
    {
        var apply = new DeltaApply<InvalidReader, DeltaChecksum>(new InvalidReader(consumed), new DeltaChecksum());
        Assert.Equal(DeltaApplyExceptionError.InvalidDelta,
            Assert.Throws<DeltaApplyException>(() => apply.Apply([], new byte[1])).Error);
    }

    public sealed class InvalidReader : IDeltaReader
    {
        private readonly uint _consumed;
        public InvalidReader() { }
        public InvalidReader(uint consumed) => _consumed = consumed;
        public bool TryRead(ReadOnlySpan<byte> input, out DeltaCommand command, out uint consumed)
        {
            command = new DeltaCommand(DeltaCommandEnum.Length, 0, 0);
            consumed = _consumed;
            return true;
        }
    }
}
