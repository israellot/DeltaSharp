using DeltaSharp.Format;
using DeltaSharp.Util;

namespace UnitTests;

public class ASCIIDeltaReaderTests
{

    public ASCIIDeltaReaderTests()
    {
        
    }

    [Fact]
    public void ReaderLength16()
    {
        var data = "l 16\n"u8;

        var reader = new ASCIIDeltaReader();
                
        var success = reader.TryRead(data,out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(5UL, consumed);
    }

    [Fact]
    public void ReaderLength256()
    {
        var data = "l 256\n"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(6UL, consumed);
    }

    [Fact]
    public void ReaderInsert16()
    {
        var data = "i 16\n"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(5UL, consumed);
    }

    [Fact]
    public void ReaderInsert256()
    {
        var data = "i 256\n"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(6UL, consumed);
    }

    [Fact]
    public void ReaderCopy16()
    {
        var data = "c 16 16\n"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(8UL, consumed);
    }

    [Fact]
    public void ReaderCopy256()
    {
        var data = "c 16 256\n"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(9UL, consumed);
    }

    [Fact]
    public void ReaderChecksum()
    {
        var data = "v 4210818301"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Checksum, command.Command);
        Assert.Equal(4210818301UL, command.Length);
        Assert.Equal(12UL, consumed);
    }

    [Fact]
    public void ReaderMultiple()
    {
        var data = "l 512\nc 16 256\ni 8\n12345678\nv 4210818301"u8;

        var reader = new ASCIIDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(512UL, command.Length);
        Assert.Equal(6UL, consumed);

        data = data.Slice((int)consumed);
        success = reader.TryRead(data, out command, out consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(9UL, consumed);

        data = data.Slice((int)consumed);
        success = reader.TryRead(data, out  command, out consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(8UL, command.Length);
        Assert.Equal(4UL, consumed);

        data = data.Slice((int)consumed+8);
        success = reader.TryRead(data, out  command, out  consumed);

        Assert.Equal(DeltaCommandEnum.Checksum, command.Command);
        Assert.Equal(4210818301UL, command.Length);
        Assert.Equal(13UL, consumed);
    }
}