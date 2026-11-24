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

    

    [Theory]
    [InlineData(2047, 8)]
    [InlineData(2048, 16)]
    [InlineData(ushort.MaxValue * 8 - 1, 16)]
#if DELTASHARP_HASHTABLE24
    [InlineData(ushort.MaxValue * 8, 24)]
    [InlineData(0xff_ff_ff * 8 - 1, 24)]
#else
    [InlineData(ushort.MaxValue * 8, 32)]
    [InlineData(0xff_ff_ff * 8 - 1, 32)]
#endif
    [InlineData(0xff_ff_ff * 8, 32)]
    public void SelectsExpectedIndexWidth(int sourceLength, int bits)
    {
        Assert.Equal(bits, (int)Hashtable64.GetIndexWidth(sourceLength));
    }

    [Fact]
    public void Hashtable32Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[0xffffff*8+1];
        random.NextBytes(data);

        using var hashtable = new Hashtable64_32(data);

        var matched = hashtable.Match(data.Slice(8));

        while (matched != 8 && matched>0)
        {
            matched = hashtable.More(matched);
        }
        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }

    [Fact]
    public void Hashtable32CountsOnlyCompleteBlocksWhenReusingPooledStorage()
    {
        // Both inputs use eight buckets. The first table writes links in slots the second never uses.
        using (var full = new Hashtable64_32(new byte[8 * 8]))
            Assert.Equal(7, full.GetStats().Collisions);

        var data = new byte[5 * 8 + 7];
        using var table = new Hashtable64_32(data);
        var stats = table.GetStats();
        Assert.Equal(8, stats.Slots);
        Assert.Equal(1, stats.SlotsFilled);
        Assert.Equal(4, stats.Collisions);

        int position = table.Match(data);
        for (int block = 0; block < 5; block++)
        {
            Assert.Equal(block * 8, position);
            position = table.More(position);
        }
        Assert.True(position < 0);
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
    [InlineData(1019)]
    [InlineData(1020)]
    [InlineData(1031)]
    [InlineData(1032)]
    [InlineData(2040)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(ushort.MaxValue * 8 - 1)]
    [InlineData(ushort.MaxValue * 8)]
    [InlineData(ushort.MaxValue * 8 + 1)]
    [InlineData(1024 * 1024)]
    public void RoundTripAcrossHashtableSizeBoundary(int length)
    {
        var source = new byte[length];
        new Random(11).NextBytes(source);
        var target = (byte[])source.Clone();
        var editSpacing = Math.Min(4096, length / 3);
        for (var i = editSpacing; i < target.Length; i += editSpacing)
            target[i] ^= 0xff;

        var patch = DeltaSharp.DeltaSharp.CreateBinary(source, target);
        var restored = DeltaSharp.DeltaSharp.ApplyBinary(source, patch.Data.Span);

        Assert.True(restored.Data.Span.SequenceEqual(target));
    }

    [Theory]
    [InlineData(2040)]
    [InlineData(2047)]
    [InlineData(2048)]
    public void HashtableTraversesEveryRepeatedBlockAtByteIndexBoundary(int length)
    {
        var data = new byte[length];
        using var hashtable = new Hashtable64(data);

        var position = hashtable.Match(data);
        for (var block = 0; block < length / 8; block++)
        {
            Assert.Equal(block * 8, position);
            position = hashtable.More(position);
        }

        Assert.True(position < 0);
    }

    [Fact]
    public void Hashtable8RejectsAnUnrepresentableBlockIndex()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            using var hashtable = new Hashtable64_8(new byte[2048]);
        });
    }

    [Fact]
    public void Hashtable16Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[ushort.MaxValue*8-1];
        random.NextBytes(data);

        using var hashtable = new Hashtable64_16(data);

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

        using var hashtable = new Hashtable64_8(data);

        var matched = hashtable.Match(data.Slice(8));
        while (matched != 8 && matched > 0)
        {
            matched = hashtable.More(matched);
        }

        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

    }



}
