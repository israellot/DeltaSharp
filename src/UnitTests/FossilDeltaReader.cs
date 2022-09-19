using DeltaSharp.Format;
using DeltaSharp.Util;
using System.Text;

namespace UnitTests;

public class FossilDeltaReaderTests
{

    public FossilDeltaReaderTests()
    {
        
    }

    [Fact]
    public void ReaderLength16()
    {
        var data = "G\n"u8;
        
        var reader = new FossilDeltaReader();
                
        var success = reader.TryRead(data,out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(2UL, consumed);
    }

    [Fact]
    public void ReaderLength256()
    {
        var data = "40\n"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(3UL, consumed);
    }

    [Fact]
    public void ReaderInsert16()
    {
        var data = "G:"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(2UL, consumed);
    }

    [Fact]
    public void ReaderInsert256()
    {
        var data = "40:"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(3UL, consumed);
    }

    [Fact]
    public void ReaderCopy16()
    {
        var data = "G@G,"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);
              
        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(4UL, consumed);
    }

    [Fact]
    public void ReaderCopy256()
    {
        var data = "40@G,"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(5UL, consumed);
    }

    [Fact]
    public void ReaderChecksum()
    {
        var data = "3vz~oy;"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Checksum, command.Command);
        Assert.Equal(4210818301UL, command.Length);
        Assert.Equal(7UL, consumed);
    }

    [Fact]
    public void ReaderMultiple()
    {
        var data = "40\n40@G,8:123456783vz~oy;"u8;

        var reader = new FossilDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(3UL, consumed);

        data = data.Slice((int)consumed);
        success = reader.TryRead(data, out command, out consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(5UL, consumed);

        data = data.Slice((int)consumed);
        success = reader.TryRead(data, out  command, out consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(8UL, command.Length);
        Assert.Equal(2UL, consumed);

        data = data.Slice((int)consumed+8);
        success = reader.TryRead(data, out  command, out  consumed);

        Assert.Equal(DeltaCommandEnum.Checksum, command.Command);
        Assert.Equal(4210818301UL, command.Length);
        Assert.Equal(7UL, consumed);
    }
}