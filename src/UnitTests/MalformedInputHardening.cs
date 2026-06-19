using DeltaSharp;
using DeltaSharp.Format;
using DeltaSharp.Util;
using System.Text;

namespace UnitTests;

// Regression tests for bounds/overflow hardening of the (untrusted) delta read path.
// Each test feeds a crafted/malformed delta that previously threw a raw
// IndexOutOfRangeException / ArgumentOutOfRangeException (or silently overflowed)
// and asserts the parser now fails cleanly via its documented contract
// (TryRead == false, or a typed DeltaReaderException / DeltaApplyException).
public class MalformedInputHardeningTests
{
    // --- FossilDeltaReader: input[0] after ReadInt consumed everything ---

    [Fact]
    public void Fossil_AllDigits_NoCommandByte_DoesNotThrowIndexOutOfRange()
    {
        // "40" is two base64 digits with no trailing command byte; ReadInt
        // consumes both, leaving the buffer empty before the command dereference.
        var data = "40"u8;
        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void Fossil_IntegerTooLarge_ThrowsTypedException()
    {
        // A long run of digits would overflow the 32-bit accumulator; reject instead
        // of silently wrapping to a corrupt length/offset.
        var data = "zzzzzzzzzz\n"u8.ToArray(); // 'z' = 62; 10 digits >> 32-bit capacity
        var reader = new FossilDeltaReader();

        Assert.Throws<DeltaReaderException>(() => reader.TryRead(data, out _, out _));
    }

    [Fact]
    public void Fossil_LengthAtCapacity_StillParses()
    {
        // Sanity: a value that fits in 32 bits must still parse (guard isn't too eager).
        var data = "G\n"u8; // 16
        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out _);

        Assert.True(success);
        Assert.Equal(16UL, command.Length);
    }

    // --- ASCIIDeltaReader: whitespace-skip loop / separator validation ---

    [Fact]
    public void Ascii_OnlyLineBreaks_DoesNotThrowIndexOutOfRange()
    {
        var data = "\n\r\n"u8;
        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void Ascii_CopyMissingSeparator_ThrowsTypedException()
    {
        // 'c' command whose position number is followed directly by the newline with
        // no whitespace separator before the (absent) length number. Must fail with a
        // typed DeltaReaderException rather than mis-slicing / misparsing.
        var data = "c 5\n"u8.ToArray();
        var reader = new ASCIIDeltaReader();

        Assert.Throws<DeltaReaderException>(() => reader.TryRead(data, out _, out _));
    }

    // --- DeltaApply: COPY position+length and INSERT length bounds ---

    [Fact]
    public void Apply_CopyRunsPastSourceEnd_ThrowsTypedException()
    {
        var deltaApply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();

        var source = "abcdef"u8.ToArray(); // length 6
        // Declare output length 4, then copy position 4 length 4 -> 4+4=8 > source 6.
        // Length (4) <= output (4) passes the old guard; position (4) <= source (6) too.
        var deltaString = "l 4\nc 4 4\n".Replace("\r\n", "\n");
        var delta = Encoding.UTF8.GetBytes(deltaString);

        Assert.Throws<DeltaApplyException>(() => deltaApply.Apply(source, delta));
    }

    [Fact]
    public void Apply_InsertLongerThanDeltaTail_ThrowsTypedException()
    {
        var deltaApply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();

        var source = "abcdef"u8.ToArray();
        // Output length 8, then insert 8 bytes but supply only 2 ("xx") in the delta.
        // Insert length (8) <= output writeSpan (8) passes the old guard.
        var deltaString = "l 8\ni 8\nxx\n".Replace("\r\n", "\n");
        var delta = Encoding.UTF8.GetBytes(deltaString);

        Assert.Throws<DeltaApplyException>(() => deltaApply.Apply(source, delta));
    }

    // --- DeltaApplyException now actually records its Error ---

    [Fact]
    public void DeltaApplyException_PreservesErrorCode()
    {
        var deltaApply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();

        var source = "abcdef"u8.ToArray();
        var deltaString = "l 4\nc 4 4\n".Replace("\r\n", "\n");
        var delta = Encoding.UTF8.GetBytes(deltaString);

        var ex = Assert.Throws<DeltaApplyException>(() => deltaApply.Apply(source, delta));
        Assert.Equal(DeltaApplyException.DeltaApplyExceptionError.InvalidCopyLength, ex.Error);
    }

    // --- VarIntFormatter.DecodeUnsafe: truncated multi-byte input is bounds-safe ---

    [Fact]
    public void DecodeUnsafe_TruncatedMultiByte_SignalsFailureNotOOB()
    {
        // For every multi-byte lead byte, supply the lead byte alone (and any other
        // truncated prefix). DecodeUnsafe must not read past the buffer; it signals
        // "could not decode" via consumed == 0 instead.
        byte[] leads = { 241, 248, 249, 250, 251, 252, 253, 254, 255 };
        foreach (var lead in leads)
        {
            var buffer = new byte[] { lead }; // declares a multi-byte value, supplies 1 byte
            VarIntFormatter.DecodeUnsafe(buffer, out var consumed);
            Assert.Equal(0, consumed);
        }
    }

    [Fact]
    public void DecodeUnsafe_CompleteInput_StillDecodes()
    {
        // Sanity: a fully-supplied encoding round-trips through DecodeUnsafe.
        void Check(ulong x)
        {
            var encoded = VarIntFormatter.Encode(x);
            var decoded = VarIntFormatter.DecodeUnsafe(encoded, out var consumed);
            Assert.True(consumed > 0);
            Assert.Equal(x, decoded);
        }

        Check(0);
        Check(241);
        Check(2288);
        Check(67824);
        Check(16777216);
        Check(4294967296);
        Check(ulong.MaxValue);
    }
}
