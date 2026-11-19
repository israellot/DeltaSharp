using DeltaSharp;
using DeltaSharp.Format;
using DeltaSharp.Util.Hashtable;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;

namespace UnitTests;

public class HashtableTests
{

    public HashtableTests()
    {
       
    }

    

    [Fact]
    public void Hashtable32Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[0xffffff*8+1];
        random.NextBytes(data);

        var hashtable = new Hashtable64_32(data);

        var matched = hashtable.Match(data.Slice(8));

        while (matched != 8 && matched>0)
        {
            matched = hashtable.More(matched);
        }
        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }

#if DELTASHARP_HASHTABLE24
    [Fact]
    public void Hashtable24Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[ushort.MaxValue*8+1];
        random.NextBytes(data);

        using var hashtable = new Hashtable64_24(data);

        var matched = hashtable.Match(data.Slice(8));

        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }
#endif

    [Theory]
    [InlineData(ushort.MaxValue * 8 - 1)]
    [InlineData(ushort.MaxValue * 8)]
    [InlineData(ushort.MaxValue * 8 + 1)]
    [InlineData(1024 * 1024)]
    public void RoundTripAcrossHashtableSizeBoundary(int length)
    {
        var source = new byte[length];
        new Random(11).NextBytes(source);
        var target = (byte[])source.Clone();
        for (var i = 4096; i < target.Length; i += 4096)
            target[i] ^= 0xff;

        var patch = DeltaSharp.DeltaSharp.CreateBinary(source, target);
        var restored = DeltaSharp.DeltaSharp.ApplyBinary(source, patch.Data.Span);

        Assert.True(restored.Data.Span.SequenceEqual(target));
    }

    [Fact]
    public void Hashtable16Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[ushort.MaxValue*8-1];
        random.NextBytes(data);

        var hashtable = new Hashtable64_16(data);

        var matched = hashtable.Match(data.Slice(8));
        while (matched != 8 && matched > 0)
        {
            matched = hashtable.More(matched);
        }

        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }

    [Fact]
    public void Hashtable8Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[(byte.MaxValue)*8];
        random.NextBytes(data);

        var hashtable = new Hashtable64_8(data);

        var matched = hashtable.Match(data.Slice(8));
        while (matched != 8 && matched > 0)
        {
            matched = hashtable.More(matched);
        }

        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }



}
