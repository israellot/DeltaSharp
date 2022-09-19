using DeltaSharp.Util;

namespace UnitTests;

public class VarIntTests
{

    public VarIntTests()
    {
        
    }

    [Fact]
    public void RoundTrip()
    {
        void Test(ulong x)
        {
            Span<byte> encoded = VarIntFormatter.Encode(x);

            var success = VarIntFormatter.TryDecode(encoded, out var decoded,out var consumed);

            Assert.True(success);
            Assert.Equal(x, decoded);
        }

        Test(0);
        Test(1);

        Test(239);
        Test(240);
        Test(241);

        Test(2286);
        Test(2287);
        Test(2288);

        Test(67822);
        Test(67823);
        Test(67824);

        Test(16777214);
        Test(16777215);
        Test(16777216);

        Test(4294967294);
        Test(4294967295);
        Test(4294967296);

        Test(1099511627774);
        Test(1099511627775);
        Test(1099511627776);

        Test(281474976710654);
        Test(281474976710655);
        Test(281474976710656);

        Test(72057594037927934);
        Test(72057594037927935);
        Test(72057594037927936);

        Test(Int64.MaxValue);
        Test(UInt64.MaxValue);

    }


}