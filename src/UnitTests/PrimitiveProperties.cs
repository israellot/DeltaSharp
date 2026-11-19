using DeltaSharp.Format;
using DeltaSharp.Util;

namespace UnitTests;

public class PrimitivePropertiesTests
{
    [Fact]
    public void ChecksumMatchesIndependentBytewiseSumForUnalignedSlices()
    {
        var random = new Random(45);
        var data = new byte[1028];
        random.NextBytes(data);
        var checksum = new DeltaChecksum();
        for (int offset = 0; offset < 4; offset++)
            for (int length = 0; length <= 1024; length++)
            {
                uint expected = 0;
                for (int i = 0; i < length; i++)
                    expected = unchecked(expected + ((uint)data[offset + i] << (24 - (i % 4) * 8)));
                Assert.Equal(expected, checksum.Checksum(data.AsSpan(offset, length)));
            }
        Assert.Equal(0xfffffffeu, checksum.Checksum(Enumerable.Repeat((byte)255, 8).ToArray()));
    }

    [Fact]
    public void SpanMatchingAgreesWithScalarReferenceAtEveryMismatchAndAlignment()
    {
        for (int length = 0; length <= 80; length++)
            for (int offset = 0; offset < 4; offset++)
                for (int mismatch = -1; mismatch < length; mismatch++)
                {
                    var left = new byte[length + offset];
                    var right = new byte[length + 7];
                    if (mismatch >= 0)
                        left[offset + mismatch] = 1;
                    var x = left.AsSpan(offset);
                    var y = right.AsSpan(3);
                    int forward = 0;
                    while (forward < Math.Min(x.Length, y.Length) && x[forward] == y[forward])
                        forward++;
                    int backward = 0;
                    while (backward < Math.Min(x.Length, y.Length) && x[^(backward + 1)] == y[^(backward + 1)])
                        backward++;
                    Assert.Equal(forward, SpanHelper.MatchForward(x, y));
                    Assert.Equal(forward, SpanHelper.MatchForward(y, x));
                    Assert.Equal(backward, SpanHelper.MatchBackward(x, y));
                    Assert.Equal(backward, SpanHelper.MatchBackward(y, x));
                }
    }

    public static IEnumerable<object[]> VarIntVectors()
    {
        yield return [0UL, "00"];
        yield return [240UL, "F0"];
        yield return [241UL, "F101"];
        yield return [2287UL, "F8FF"];
        yield return [2288UL, "F90000"];
        yield return [67823UL, "F9FFFF"];
        yield return [67824UL, "FA0108F0"];
        yield return [16777215UL, "FAFFFFFF"];
        yield return [16777216UL, "FB01000000"];
        yield return [4294967295UL, "FBFFFFFFFF"];
        yield return [4294967296UL, "FC0100000000"];
        yield return [1099511627775UL, "FCFFFFFFFFFF"];
        yield return [1099511627776UL, "FD010000000000"];
        yield return [281474976710655UL, "FDFFFFFFFFFFFF"];
        yield return [281474976710656UL, "FE01000000000000"];
        yield return [72057594037927935UL, "FEFFFFFFFFFFFFFF"];
        yield return [72057594037927936UL, "FF0100000000000000"];
        yield return [ulong.MaxValue, "FFFFFFFFFFFFFFFFFF"];
    }

    [Theory]
    [MemberData(nameof(VarIntVectors))]
    public void VarIntsMatchWireVectorsAndRejectEveryShortBuffer(ulong value, string hex)
    {
        var expected = Convert.FromHexString(hex);
        var buffer = Enumerable.Repeat((byte)0xa5, 12).ToArray();
        Assert.True(VarIntFormatter.TryEncode(value, buffer.AsSpan(1), out int written));
        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, buffer.AsSpan(1, written).ToArray());
        Assert.Equal(0xa5, buffer[0]);
        Assert.All(buffer.Skip(written + 1), b => Assert.Equal(0xa5, b));
        Assert.Equal(expected.Length, VarIntFormatter.EncodeUnsafe(value, buffer));
        Assert.Equal(expected, buffer.AsSpan(0, expected.Length).ToArray());
        Assert.True(VarIntFormatter.TryDecode(expected, out var decoded, out var consumed));
        Assert.Equal(value, decoded);
        Assert.Equal(expected.Length, consumed);
        Assert.Equal(value, VarIntFormatter.DecodeUnsafe(expected, out consumed));
        Assert.Equal(expected.Length, consumed);

        for (int length = 0; length < expected.Length; length++)
        {
            var shortBuffer = Enumerable.Repeat((byte)0xa5, length).ToArray();
            Assert.False(VarIntFormatter.TryEncode(value, shortBuffer, out written));
            Assert.Equal(0, written);
            Assert.All(shortBuffer, b => Assert.Equal(0xa5, b));
            Assert.False(VarIntFormatter.TryDecode(expected.AsSpan(0, length), out decoded, out consumed));
            Assert.Equal(0, consumed);
            Assert.Equal(0UL, decoded);
            Assert.Equal(0UL, VarIntFormatter.DecodeUnsafe(expected.AsSpan(0, length), out consumed));
            Assert.Equal(0, consumed);
        }
    }

    [Fact]
    public void VarIntDecodersAgreeForEveryLeadingByte()
    {
        for (int lead = 0; lead <= 255; lead++)
            for (int length = 0; length <= 10; length++)
            {
                var buffer = Enumerable.Repeat((byte)0xa7, length).ToArray();
                if (length > 0)
                    buffer[0] = (byte)lead;
                bool success = VarIntFormatter.TryDecode(buffer, out var value, out var consumed);
                var unsafeValue = VarIntFormatter.DecodeUnsafe(buffer, out var unsafeConsumed);
                Assert.Equal(success, unsafeConsumed != 0);
                Assert.Equal(consumed, unsafeConsumed);
                Assert.Equal(value, unsafeValue);
            }
    }
}
