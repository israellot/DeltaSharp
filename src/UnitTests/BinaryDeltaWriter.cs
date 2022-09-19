using DeltaSharp.Format;
using DeltaSharp.Util;

namespace UnitTests;

public class DeltaWriterTests
{

    public DeltaWriterTests()
    {
        
    }

    [Fact]
    public void WritePureInsert()
    {
        var data = "0123456789ABCDEF";
        byte[] shouldEqualTo = [
            (byte)'l',16,
            (byte)'i',16,
            (byte)'0',(byte)'1',(byte)'2',(byte)'3',(byte)'4',(byte)'5',(byte)'6',(byte)'7',
            (byte)'8',(byte)'9',(byte)'A',(byte)'B',(byte)'C',(byte)'D',(byte)'E',(byte)'F',
            (byte)'v',251, 0xfa,0xfb,0xfc,0xfd
        ];

        var writer = new BinaryDeltaWriter();

        writer.WriteLength((uint)data.Length);
        writer.WriteInsert("0123456789ABCDEF"u8);
        writer.WriteChecksum(0xfafbfcfd);

        var encoded = writer.GetOutput().ToArray();

        Assert.Equal(shouldEqualTo, encoded);
        
    }


}