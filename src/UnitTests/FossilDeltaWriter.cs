using DeltaSharp.Format;
using DeltaSharp.Util;
using System.Text;

namespace UnitTests;

public class FossilDeltaWriterTest
{

    public FossilDeltaWriterTest()
    {
        
    }

    [Fact]
    public void WriteMixed()
    {
        var shouldEqualTo = "G\nG:0123456789ABCDEF10@_,3vz~oy;";

        var writer = new FossilDeltaWriter();

        writer.WriteLength(16);
        writer.WriteInsert("0123456789ABCDEF"u8);
        writer.WriteCopy(36, 64);
        writer.WriteChecksum(0xfafbfcfd);

        var encoded = writer.GetOutput().ToArray();

        var encodedString = Encoding.UTF8.GetString(encoded);

        Assert.Equal(shouldEqualTo, encodedString);
        
    }


}