using DeltaSharp;

namespace UnitTests;

public class TargetGeneratorTests
{
    [Fact]
    public void MultilineScriptsAllowBlankLinesAndTrailingSeparators()
    {
        var generator = new TargetGenerator(new[] { "first", "second", "third" });
        generator.Execute("\nSWP 0 2;\n\nDEL 1 1;\nADD -1 last;\nSET 0 changed;\n");
        Assert.Equal(new[] { "changed", "first", "last" }, generator.SourceLines);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    public void HashOperationPreservesLengthForShortAndLongLines(int length)
    {
        var input = new string('a', length);
        var generator = new TargetGenerator(new[] { input });
        generator.Execute("SET 0 HASH");
        Assert.Equal(length, generator.SourceLines[0].Length);
        if (length > 0)
            Assert.NotEqual(input, generator.SourceLines[0]);
        var repeat = new TargetGenerator(new[] { input });
        repeat.Execute("SET 0 HASH");
        Assert.Equal(generator.SourceLines, repeat.SourceLines);
    }

    [Fact]
    public void SeededRandomOperationsAreReproducible()
    {
        var first = new TargetGenerator(random: new Random(12));
        var second = new TargetGenerator(random: new Random(12));
        const string script = "ADD 0 RND(15) 8;SET 0 RND 3 2;SWP 0 4 4;DEL 1 2";
        first.Execute(script);
        second.Execute(script);
        Assert.Equal(first.Source, second.Source);
        Assert.Equal(6, first.SourceLines.Count);
    }
}
