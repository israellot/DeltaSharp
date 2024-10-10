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

    [Fact]
    public void Hashtable24Match()
    {
        var random = new Random(11);
        Span<byte> data = new byte[ushort.MaxValue*8+1];
        random.NextBytes(data);

        var hashtable = new Hashtable64_24(data);

        var matched = hashtable.Match(data.Slice(8));

        var stats = hashtable.GetStats();

        Assert.Equal(8, matched);

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