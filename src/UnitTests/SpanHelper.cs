using DeltaSharp;
using DeltaSharp.Format;
using DeltaSharp.Util;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;

namespace UnitTests;

public class SpanHelperTests
{

    public SpanHelperTests()
    {
       
    }


    [Fact]
    public void MatchBackwardLessThan16()
    {
        
        for (var i = 0; i <= 16; i++)
        {
            var s1 = new byte[16];
            var s2 = new byte[16];
            if(i<16)
                s1[15-i] = (byte)(i + 1);
            var result = SpanHelper.MatchBackward(s1, s2);
            Assert.Equal(i, result);
        }
        
    }

    [Fact]
    public void MatchBackwardMoreThan16()
    {
        for(var i = 0; i < 45; i++)
        {
            var s1 = new byte[45];
            var s2 = new byte[45];
            if (i < 45)
                s1[44 - i] = (byte)(i + 1);
            var result = SpanHelper.MatchBackward(s1, s2);
            Assert.Equal(i, result);
        }       
    }

    [Fact]
    public void MatchForwardMoreThan16()
    {
        var s1 = "0000000000000000000000000000"u8;
        var s2 = "0000000000000000000000000000"u8;
        var result = SpanHelper.MatchForward(s1, s2);
        Assert.Equal(28, result);

        s1 = "000000000000000000000000000"u8;
        s2 = "100000000000000000000000000"u8;
        result = SpanHelper.MatchForward(s1, s2);
        Assert.Equal(0, result);

        s1 = "0000000000000000000000000000"u8;
        s2 = "0000100000000000000000000000"u8;
        result = SpanHelper.MatchForward(s1, s2);
        Assert.Equal(4, result);

        s1 = "0000000000000000000000000000"u8;
        s2 = "0000000000000000000000000001"u8;
        result = SpanHelper.MatchForward(s1, s2);
        Assert.Equal(27, result);
    }

    [Fact]
    public void MatchForwardLessThan16()
    {
        for (var i = 0; i <= 16; i++)
        {
            var s1 = new byte[16];
            var s2 = new byte[16];

            if(i<16)
                s1[i] = 1;

            var result = SpanHelper.MatchForward(s1, s2);
            Assert.Equal(i, result);
        }
    }
}