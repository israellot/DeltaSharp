using DeltaSharp.Format;
using DeltaSharp.Util;

namespace UnitTests;

public class DeltaReaderTests
{

    public DeltaReaderTests()
    {
        
    }

    [Fact]
    public void ReaderLength16()
    {       
        byte[] data = [(byte)'l',16];

        var reader = new BinaryDeltaReader();
                
        var success = reader.TryRead(data,out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(2UL, consumed);
    }

    [Fact]
    public void ReaderLength256()
    {
        byte[] data = [(byte)'l', 241,16];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Length, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(3UL, consumed);
    }

    [Fact]
    public void ReaderInsert16()
    {
        byte[] data = [(byte)'i', 16];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(2UL, consumed);
    }

    [Fact]
    public void ReaderInsert256()
    {
        byte[] data = [(byte)'i', 241, 16];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Insert, command.Command);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(3UL, consumed);
    }

    [Fact]
    public void ReaderCopy16()
    {
        byte[] data = [(byte)'c',16, 16];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(16UL, command.Length);
        Assert.Equal(3UL, consumed);
    }

    [Fact]
    public void ReaderCopy256()
    {
        byte[] data = [(byte)'c',16, 241, 16];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Copy, command.Command);
        Assert.Equal(16UL, command.Position);
        Assert.Equal(256UL, command.Length);
        Assert.Equal(4UL, consumed);
    }

    [Fact]
    public void ReaderChecksum()
    {
        byte[] data = [(byte)'v',251, 0xfa, 0xfb, 0xfc, 0xfd];

        var reader = new BinaryDeltaReader();

        var success = reader.TryRead(data, out var command, out var consumed);

        Assert.Equal(DeltaCommandEnum.Checksum, command.Command);
        Assert.Equal(0xfafbfcfd, command.Length);
        Assert.Equal(6UL, consumed);
    }

}