using DeltaSharp;
using DeltaSharp.Format;
using System.Text;
using static DeltaSharp.DeltaApplyException;

namespace UnitTests;

public class AuditRegressionTests
{
    internal static IDelta Format(string format) => format switch
    {
        "Binary" => DeltaSharp.DeltaSharp.Binary,
        "ASCII" => DeltaSharp.DeltaSharp.ASCII,
        "Fossil" => DeltaSharp.DeltaSharp.Fossil,
        _ => throw new ArgumentException(nameof(format))
    };

    internal static IDeltaWriter Writer(string format) => format switch
    {
        "Binary" => new BinaryDeltaWriter(),
        "ASCII" => new ASCIIDeltaWriter(),
        "Fossil" => new FossilDeltaWriter(),
        _ => throw new ArgumentException(nameof(format))
    };

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void EmptyTargetRoundTrips(string format)
    {
        var codec = Format(format);
        foreach (var source in new[] { Array.Empty<byte>(), "original source"u8.ToArray() })
        {
            var patch = codec.Create.Create(source, ReadOnlySpan<byte>.Empty);
            Assert.Empty(codec.Apply.Apply(source, patch.Data.Span).Data.ToArray());
            Assert.Equal(0u, patch.Crc32);
        }
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void ChecksumCannotSubstituteForMissingOutput(string format)
    {
        using var writer = Writer(format);
        // The checksum matches the pre-existing destination, but no command writes it.
        var output = "secret"u8.ToArray();
        writer.WriteLength((ulong)output.Length);
        writer.WriteChecksum(new DeltaChecksum().Checksum(output));
        var ex = Assert.Throws<DeltaApplyException>(() =>
            Format(format).Apply.Apply([], writer.GetOutput().Span, output));
        Assert.Equal(DeltaApplyExceptionError.InvalidOutputLength, ex.Error);
    }

    [Theory]
    [InlineData("Binary", 0)]
    [InlineData("Binary", 8)]
    [InlineData("ASCII", 0)]
    [InlineData("ASCII", 8)]
    [InlineData("Fossil", 0)]
    [InlineData("Fossil", 8)]
    public void RepeatedLengthHeaderIsRejected(string format, int secondLength)
    {
        using var writer = Writer(format);
        writer.WriteLength(4);
        writer.WriteLength((ulong)secondLength);
        writer.WriteChecksum(0);
        var ex = Assert.Throws<DeltaApplyException>(() =>
            Format(format).Apply.Apply([], writer.GetOutput().Span));
        Assert.Equal(DeltaApplyExceptionError.InvalidDelta, ex.Error);
    }

    [Theory]
    [InlineData("l 4x\n")]
    [InlineData("c 0 4x\n")]
    [InlineData("i 4x\n")]
    [InlineData("v 0x")]
    public void AsciiRejectsInvalidNumericTerminators(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        Assert.Throws<DeltaReaderException>(() => new ASCIIDeltaReader().TryRead(bytes, out _, out _));
    }

    [Theory]
    [InlineData("l 4\r\n", DeltaCommandEnum.Length, 5)]
    [InlineData("c 0 4\r\n", DeltaCommandEnum.Copy, 7)]
    [InlineData("i 4\r\nDATA", DeltaCommandEnum.Insert, 5)]
    [InlineData("i 4 DATA", DeltaCommandEnum.Insert, 4)]
    public void AsciiConsumesTheActualDelimiter(string text, DeltaCommandEnum expected, uint expectedConsumed)
    {
        Assert.True(new ASCIIDeltaReader().TryRead(Encoding.ASCII.GetBytes(text), out var cmd, out var consumed));
        Assert.Equal(expected, cmd.Command);
        Assert.Equal(4UL, cmd.Length);
        Assert.Equal(expectedConsumed, consumed);
    }

    [Fact]
    public void CustomWriterOnlyNeedsThePublicGenericConstructorContract()
    {
        var creator = new DeltaCreate<ParameterlessWriter, DeltaChecksum>();
        var target = "custom writer"u8.ToArray();
        var patch = creator.Create([], target);
        Assert.Equal(target, DeltaSharp.DeltaSharp.ApplyBinary([], patch.Data.Span).Data.ToArray());
    }

    public sealed class ParameterlessWriter : IDeltaWriter
    {
        private readonly BinaryDeltaWriter _inner = new();
        public void WriteLength(ulong length) => _inner.WriteLength(length);
        public void WriteCopy(ulong position, ulong length) => _inner.WriteCopy(position, length);
        public void WriteInsert(ReadOnlySpan<byte> data) => _inner.WriteInsert(data);
        public void WriteChecksum(uint checksum) => _inner.WriteChecksum(checksum);
        public ReadOnlyMemory<byte> GetOutput() => _inner.GetOutput();
        public void Dispose() => _inner.Dispose();
    }

    [Fact]
    public void CreateExceptionPreservesTheSuppliedError()
    {
        var error = DeltaCreateException.DeltaCreateExceptionError.InvalidChecksum;
        Assert.Equal(error, new DeltaCreateException(error).Error);
    }

    [Fact]
    public void FossilAcceptsEmptyInsertFromReferenceEncoder()
    {
        // Fossil's small-source encoder emits an empty literal for an empty target.
        Assert.Empty(DeltaSharp.DeltaSharp.ApplyFossil([], "0\n0:0;"u8).Data.ToArray());
    }

    [Fact]
    public void FossilRejectsHighBitAliasesOfAsciiDigits()
    {
        var bytes = new byte[] { 0xb0, (byte)'\n' }; // 0xb0 must not alias ASCII '0'.
        Assert.False(new FossilDeltaReader().TryRead(bytes, out _, out _));
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void OutputCanOverlapSourceWithoutChangingLaterCopies(string format)
    {
        var source = "AAAABBBB"u8.ToArray();
        var target = "BBBBAAAA"u8.ToArray();
        using var writer = Writer(format);
        writer.WriteLength(8);
        writer.WriteCopy(4, 4);
        writer.WriteCopy(0, 4);
        writer.WriteChecksum(new DeltaChecksum().Checksum(target));
        var result = Format(format).Apply.Apply(source, writer.GetOutput().Span, source);
        Assert.Equal(target, result.Data.ToArray());
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void OutputCanOverlapUnreadPatchCommands(string format)
    {
        var source = "abcdefghijklmnop"u8.ToArray();
        using var writer = Writer(format);
        writer.WriteLength(16);
        writer.WriteCopy(0, 16);
        writer.WriteChecksum(new DeltaChecksum().Checksum(source));
        var patch = writer.GetOutput().ToArray();
        var buffer = new byte[Math.Max(16, patch.Length)];
        patch.CopyTo(buffer, 0);
        var result = Format(format).Apply.Apply(source, buffer.AsSpan(0, patch.Length), buffer);
        Assert.Equal(source, result.Data.ToArray());
    }
}
