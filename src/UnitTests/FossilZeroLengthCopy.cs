using DeltaSharp;
using DeltaSharp.Format;
using System.Text;
using static DeltaSharp.DeltaApplyException;

namespace UnitTests;

public class FossilZeroLengthCopyTests
{
    [Theory]
    [InlineData("0@2,3:abc", 2UL, 4u)]
    [InlineData("0@40,", 256UL, 5u)]
    public void ReaderPreservesZeroLengthAndConsumesTheWholeCommand(string patch, ulong offset, uint expectedConsumed)
    {
        Assert.True(new FossilDeltaReader().TryRead(Encoding.ASCII.GetBytes(patch), out var command, out var consumed));
        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(0UL, command.Length);
        Assert.Equal(offset, command.Position);
        Assert.Equal(expectedConsumed, consumed);
    }

    [Theory]
    [InlineData("", "0\n0@0,0;", "")]
    [InlineData("ABCDEFGH", "0\n0@2,0;", "")]
    [InlineData("ABCDEFGH", "0\n0@8,0;", "")]
    [InlineData("abcdef", "3\n0@2,1@0,0@6,2:bc0@1,1XObC0;", "abc")]
    public void ApplyTreatsZeroLengthCopyAsNoOp(string sourceText, string patchText, string expectedText)
    {
        var source = Encoding.ASCII.GetBytes(sourceText);
        var patch = Encoding.ASCII.GetBytes(patchText);
        var expected = Encoding.ASCII.GetBytes(expectedText);
        var result = DeltaSharp.DeltaSharp.ApplyFossil(source, patch);
        Assert.Equal(expected, result.Data.ToArray());
        Assert.Equal(expected.Length == 0 ? 0u : 0x61626300u, result.Crc32);

        var output = Enumerable.Repeat((byte)0xa5, expected.Length + 2).ToArray();
        var buffered = DeltaSharp.DeltaSharp.Fossil.Apply.Apply(source, patch, output.AsMemory(1));
        Assert.Equal(expected, buffered.Data.ToArray());
        Assert.Equal(result.Crc32, buffered.Crc32);
        Assert.Equal(0xa5, output[0]);
        Assert.Equal(0xa5, output[^1]);
    }

    [Theory]
    // A zero-length copy cannot supply the six bytes promised by the header.
    [InlineData("6\n0@2,0;", DeltaApplyExceptionError.InvalidOutputLength)]
    [InlineData("0\n0@2,1;", DeltaApplyExceptionError.InvalidChecksum)]
    [InlineData("3\n3:abc0@2,0;", DeltaApplyExceptionError.InvalidChecksum)]
    [InlineData("0\n0@2,", DeltaApplyExceptionError.IncompleteDelta)]
    [InlineData("0\n0@9,0;", DeltaApplyExceptionError.InvalidCopyPosition)]
    public void NoOpDoesNotBypassPatchValidation(string patchText, DeltaApplyExceptionError expectedError)
    {
        var source = "ABCDEFGH"u8.ToArray();
        var patch = Encoding.ASCII.GetBytes(patchText);
        Assert.Equal(expectedError, Assert.Throws<DeltaApplyException>(() =>
            DeltaSharp.DeltaSharp.ApplyFossil(source, patch)).Error);
        Assert.Equal(expectedError, Assert.Throws<DeltaApplyException>(() =>
            DeltaSharp.DeltaSharp.Fossil.Apply.Apply(source, patch, new byte[8])).Error);
    }

    [Theory]
    [InlineData("0@2")]
    [InlineData("0@2;")]
    public void ZeroLengthCopyStillRequiresTheCommaTerminator(string patchText)
    {
        var patch = Encoding.ASCII.GetBytes(patchText);
        Assert.Throws<DeltaReaderException>(() => new FossilDeltaReader().TryRead(patch, out _, out _));
    }
}
