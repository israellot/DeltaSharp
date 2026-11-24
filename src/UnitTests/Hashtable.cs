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
    public void Hashtable32TraversesMixedTagsInSourceOrder()
    {
        byte[] firstBytes = [0x0f, 0x00, 0x08, 0xff];
        var source = new byte[firstBytes.Length * 8];
        for (int block = 0; block < firstBytes.Length; block++)
            SeedInBucket(firstBytes[block], 0, 3).CopyTo(source, block * 8);

        using var table = new Hashtable64_32(source);
        int position = table.Match(source);
        for (int block = 0; block < firstBytes.Length; block++)
        {
            Assert.Equal(block * 8, position);
            position = table.More(position);
        }

        Assert.True(position < 0);
        Assert.True(table.Match(SeedInBucket(0xff, 1, 3)) < 0);

        foreach (int queryBlock in new[] { 2, 3 })
        {
            var query = source.AsSpan(queryBlock * 8, 8);
            position = table.FirstCandidate(query, out int tag, out bool matches);
            for (int block = 0; block < firstBytes.Length; block++)
            {
                Assert.Equal(block * 8, position);
                Assert.Equal((firstBytes[block] & 0x0f) == (query[0] & 0x0f), matches);
                position = table.NextCandidate(position, tag, out matches);
            }
            Assert.True(position < 0);
        }

        var stats = table.GetStats();
        Assert.Equal(1, stats.SlotsFilled);
        Assert.Equal(3, stats.Collisions);
    }

    internal static byte[] SeedInBucket(byte firstByte, ulong bucket, ulong mask)
    {
        var seed = new byte[8];
        seed[0] = firstByte;
        for (int salt = 0; salt < 1_000_000; salt++)
        {
            BitConverter.TryWriteBytes(seed.AsSpan(1), salt);
            if ((Hash.Hash64(BitConverter.ToUInt64(seed)) & mask) == bucket)
                return seed;
        }
        throw new InvalidOperationException("Could not generate a seed for the requested test bucket.");
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
