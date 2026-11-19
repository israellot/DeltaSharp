using DeltaSharp;
using DeltaSharp.Format;
using static DeltaSharp.DeltaApplyException;

namespace UnitTests;

public class DeltaOutputLimitTests
{
    private static DeltaResult ApplyStatic(string format, byte[] source, byte[] patch, int limit) => format switch
    {
        "Binary" => DeltaSharp.DeltaSharp.ApplyBinary(source, patch, maxOutputLength: limit),
        "ASCII" => DeltaSharp.DeltaSharp.ApplyASCII(source, patch, maxOutputLength: limit),
        "Fossil" => DeltaSharp.DeltaSharp.ApplyFossil(source, patch, maxOutputLength: limit),
        _ => throw new ArgumentException(nameof(format))
    };

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void OutputAtOrBelowLimitMatchesExistingOverload(string format)
    {
        var codec = AuditRegressionTests.Format(format);
        var source = "original"u8.ToArray();
        foreach (var target in new[] { Array.Empty<byte>(), "abc"u8.ToArray() })
        {
            var patch = codec.Create.Create(source, target).Data.ToArray();
            var original = codec.Apply.Apply(source, patch);
            foreach (int limit in new[] { target.Length, target.Length + 1 })
            {
                var result = ApplyStatic(format, source, patch, limit);
                Assert.Equal(target, result.Data.ToArray());
                Assert.Equal(original.Crc32, result.Crc32);
                var viaInterface = codec.Apply.Apply(source, patch, maxOutputLength: limit);
                Assert.Equal(target, viaInterface.Data.ToArray());
                Assert.Equal(original.Crc32, viaInterface.Crc32);
            }
        }
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void ValidPatchOneByteOverLimitIsRejected(string format)
    {
        var codec = AuditRegressionTests.Format(format);
        foreach (int length in new[] { 1, 3 })
        {
            var patch = codec.Create.Create([], new byte[length]).Data.ToArray();
            Assert.Equal(DeltaApplyExceptionError.OutputLengthLimitExceeded,
                Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], patch, length - 1)).Error);
            Assert.Equal(DeltaApplyExceptionError.OutputLengthLimitExceeded,
                Assert.Throws<DeltaApplyException>(() => codec.Apply.Apply([], patch, maxOutputLength: length - 1)).Error);
        }
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void NegativeLimitIsRejectedBeforeReadingPatch(string format)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ApplyStatic(format, [], [], -1));
        Assert.Equal("maxOutputLength", ex.ParamName);
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void TinyHeaderDeclaringHugeOutputIsRejected(string format)
    {
        foreach (ulong length in new[] { (ulong)int.MaxValue, uint.MaxValue })
        {
            using var writer = AuditRegressionTests.Writer(format);
            writer.WriteLength(length);
            var patch = writer.GetOutput().ToArray();
            Assert.True(patch.Length < 32);
            Assert.Equal(DeltaApplyExceptionError.OutputLengthLimitExceeded,
                Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], patch, 1024)).Error);
        }
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void RejectionDoesNotAllocateTheDeclaredOutputBuffer(string format)
    {
        using var writer = AuditRegressionTests.Writer(format);
        writer.WriteLength(16 * 1024 * 1024);
        var patch = writer.GetOutput().ToArray();
        // Warm up the exception path, then measure only the call on this thread.
        Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], patch, 1024));
        long before = GC.GetAllocatedBytesForCurrentThread();
        DeltaApplyException? error = null;
        try
        {
            ApplyStatic(format, [], patch, 1024);
        }
        catch (DeltaApplyException ex)
        {
            error = ex;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotNull(error);
        Assert.Equal(DeltaApplyExceptionError.OutputLengthLimitExceeded, error.Error);
        Assert.True(allocated < 128 * 1024, $"Rejected header allocated {allocated} bytes.");
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void LimitedApplyStillValidatesThePatch(string format)
    {
        using var writer = AuditRegressionTests.Writer(format);
        writer.WriteLength(3);
        var incomplete = writer.GetOutput().ToArray();
        Assert.Equal(DeltaApplyExceptionError.IncompleteDelta,
            Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], incomplete, 3)).Error);
        writer.WriteInsert("abc"u8);
        writer.WriteChecksum(0); // Incorrect for "abc".
        var corrupt = writer.GetOutput().ToArray();
        Assert.Equal(DeltaApplyExceptionError.InvalidChecksum,
            Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], corrupt, 3)).Error);

        using var shortWriter = AuditRegressionTests.Writer(format);
        shortWriter.WriteLength(3);
        shortWriter.WriteChecksum(0);
        var missingOutput = shortWriter.GetOutput().ToArray();
        Assert.Equal(DeltaApplyExceptionError.InvalidOutputLength,
            Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], missingOutput, 3)).Error);
        Assert.Equal(DeltaApplyExceptionError.InvalidDelta,
            Assert.Throws<DeltaApplyException>(() => ApplyStatic(format, [], [], 3)).Error);
    }

    [Fact]
    public void ExistingInterfaceImplementationCanUseTheLimitWithoutImplementingNewMembers()
    {
        IDeltaApply apply = new ExistingApply();
        var patch = "abc"u8.ToArray();
        Assert.Equal(patch, apply.Apply([], patch, maxOutputLength: 3).Data.ToArray());
        Assert.Equal(DeltaApplyExceptionError.OutputLengthLimitExceeded,
            Assert.Throws<DeltaApplyException>(() => apply.Apply([], patch, maxOutputLength: 2)).Error);
    }

    private sealed class ExistingApply : IDeltaApply
    {
        public ulong GetOutputLength(ReadOnlySpan<byte> delta) => (ulong)delta.Length;
        public DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta)
            => throw new InvalidOperationException("The bounded overload must use caller-provided memory.");
        public DeltaResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delta, Memory<byte> output)
        {
            Assert.Equal(delta.Length, output.Length);
            delta.CopyTo(output.Span);
            return new DeltaResult { Data = output };
        }
    }
}
