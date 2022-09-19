using DeltaSharp;
using DeltaSharp.Format;
using System.Text;

namespace UnitTests;

public class DeltaApplyTests
{

    public DeltaApplyTests()
    {
       
    }

    [Fact]
    public void Single()
    {
        var deltaApply = new DeltaApply<ASCIIDeltaReader, DeltaChecksum>();

        var source = "abcdef"u8;
        var target = "abcxxdef"u8;

        var targetChecksum = new DeltaChecksum().Checksum(target);

        var deltaString = $"""
            l 8
            c 0 3
            i 2
            xx
            c 3 3
            v {targetChecksum}
            """.Replace("\r\n", "\n");

        var delta = Encoding.UTF8.GetBytes(deltaString);

        var result = deltaApply.Apply(source, delta);

        Assert.True(result.Span.SequenceEqual(target));

    }




}