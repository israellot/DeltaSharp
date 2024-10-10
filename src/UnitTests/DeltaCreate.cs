using DeltaSharp;
using DeltaSharp.Format;
using DeltaSharp.Util;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;

namespace UnitTests;

public class DeltaCreateTests
{

    public DeltaCreateTests()
    {
       
    }

    [Fact]
    public void AppendASCII()
    {
        var deltaCreate = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>();

        var os = "nfqiu98qo104;bt;vb9h[0jugatjeisl432@$oqnrwiptu";

        var append = "bqoiub87y`08r23rpu2hriub23r";

        var ts = $"{os}{append}";

        var origin = Encoding.UTF8.GetBytes(os);
        var target = Encoding.UTF8.GetBytes(ts);

        var checksum = new DeltaChecksum().Checksum(target);

        var delta = deltaCreate.Create(origin, target);

        var deltaString = Encoding.UTF8.GetString(delta.Data.Span);
        var shouldEqual = $"""
            l {target.Length}
            c 0 {origin.Length}
            i {append.Length} {append}
            v {checksum}
            """.Replace("\r\n", "\n");

        Assert.Equal(deltaString, shouldEqual);
    }

    [Fact]
    public void PermutateASCII()
    {
        var deltaCreate = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>();

        var os = "nfqiu98qo104;bt;vb9h[0jugatjeisl432@$oqnrwiptu";
        var ts = $"{os.Substring(os.Length / 2)}{os.Substring(0, os.Length / 2)}";

        var origin = Encoding.UTF8.GetBytes(os);
        var target = Encoding.UTF8.GetBytes(ts);

        var checksum = new DeltaChecksum().Checksum(target);

        var delta = deltaCreate.Create(origin, target);

        var deltaString = Encoding.UTF8.GetString(delta.Data.Span);
        var shouldEqual = $"""
            l {target.Length}
            c {target.Length / 2} {target.Length / 2}
            c {0} {target.Length / 2}
            v {checksum}
            """.Replace("\r\n", "\n");

        Assert.Equal(deltaString, shouldEqual);
    }

    [Fact]
    public void SingleInsertASCII()
    {
        var deltaCreate = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>();

        var random = new Random(11);

        Span<byte> source = new byte[1024];
        for (var i = 0; i < source.Length; i++)
            source[i] = (byte)random.Next(32, 128);//printable chars only

        string insert = "insert_test";

        Span<byte> target = new byte[1024 + insert.Length];

        source.Slice(0, 512).CopyTo(target);
        Encoding.UTF8.GetBytes(insert).CopyTo(target.Slice(512));
        source.Slice(512, 512).CopyTo(target.Slice(512 + insert.Length));

        var checksum = new DeltaChecksum().Checksum(target);

        var result = deltaCreate.Create(source, target);

        var deltaString = Encoding.UTF8.GetString(result.Data.Span);

        var shouldEqual = $""""
            l {target.Length}
            c 0 512
            i {insert.Length} {insert}
            c 512 512
            v {checksum}
            """".Replace("\r\n", "\n");

        Assert.Equal(shouldEqual,deltaString);

    }

    [Fact]
    public void SingleDeleteASCII()
    {
        var deltaCreate = new DeltaCreate<ASCIIDeltaWriter, DeltaChecksum>();

        var random = new Random(11);

        Span<byte> source = new byte[1024];
        for (var i = 0; i < source.Length; i++)
            source[i] = (byte)random.Next(32, 128);//printable chars only

        
        Span<byte> target = new byte[1023];

        source.Slice(0, 500).CopyTo(target);
        source.Slice(501).CopyTo(target.Slice(500));

        var checksum = new DeltaChecksum().Checksum(target);

        var result = deltaCreate.Create(source, target);

        var deltaString = Encoding.UTF8.GetString(result.Data.Span);

        var shouldEqual = $""""
            l {target.Length}
            c 0 500
            c 501 523
            v {checksum}
            """".Replace("\r\n", "\n");

        Assert.Equal(shouldEqual, deltaString);

    }

    [Fact]
    public void SingleInsertBinary()
    {
        var deltaCreate = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>();

        var random = new Random(11);

        Span<byte> source = new byte[1024];
        for (var i = 0; i < source.Length; i++)
            source[i] = (byte)random.Next(32, 128);//printable chars only

        string insert = "insert_test";

        Span<byte> target = new byte[1024 + insert.Length];

        source.Slice(0, 512).CopyTo(target);
        Encoding.UTF8.GetBytes(insert).CopyTo(target.Slice(512));
        source.Slice(512, 512).CopyTo(target.Slice(512 + insert.Length));

        var result = deltaCreate.Create(source, target);

        var base64Delta = Convert.ToBase64String(result.Data.Span);

        var shouldEqual = "bPQbYwDyEGkLaW5zZXJ0X3Rlc3Rj8hDyEHb7dBSaFw==";

        Assert.Equal(shouldEqual, base64Delta);

    }


}