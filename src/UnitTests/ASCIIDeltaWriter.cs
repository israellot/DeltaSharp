using DeltaSharp.Format;
using DeltaSharp.Util;
using System.Text;

namespace UnitTests;

public class ASCIIDeltaWriterTest
{

    public ASCIIDeltaWriterTest()
    {
        
    }

    [Fact]
    public void WriteMixed()
    {
        var shouldEqualTo = """
            l 16
            i 16 0123456789ABCDEF
            c 36 64
            v 4210818301
            """.Replace("\r\n", "\n");

        var writer = new ASCIIDeltaWriter();

        writer.WriteLength(16);
        writer.WriteInsert("0123456789ABCDEF"u8);
        writer.WriteCopy(36, 64);
        writer.WriteChecksum(0xfafbfcfd);

        var encoded = writer.GetOutput().ToArray();

        var encodedString = Encoding.UTF8.GetString(encoded);

        Assert.Equal(shouldEqualTo, encodedString);
        
    }


}