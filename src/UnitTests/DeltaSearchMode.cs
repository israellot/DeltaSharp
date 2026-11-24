using DeltaSharp;
using DeltaSharp.Format;
using DeltaSharp.Util.Hashtable;
using System.Text;
using Delta = DeltaSharp.DeltaSharp;

namespace UnitTests;

public class DeltaSearchModeTests
{
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    public void BoundedSearchCountsCandidatesWithDifferentFirstByteTags(int decoys)
    {
        var source = new byte[512 * 1024];
        var target = Encoding.ASCII.GetBytes(new string('O', 17));
        var mask = (ulong)(Hash.TableSize(source.Length / 8) - 1);
        var bucket = Hash.Hash64(BitConverter.ToUInt64(target)) & mask;
        var decoy = HashtableTests.SeedInBucket(0, bucket, mask);
        for (int block = 0; block < decoys; block++)
            decoy.CopyTo(source, block * 8);
        target.CopyTo(source, decoys * 8);

        // Every target window is identical, so a later lookup cannot recover
        // a match beyond the bounded search's 256th candidate.
        var copy = $"l 17\nc {decoys * 8} 17\nv ";
        var insert = $"l 17\ni 17 {Encoding.ASCII.GetString(target)}\nv ";
        var exhaustive = Delta.CreateASCII(source, target, DeltaSearchMode.Exhaustive);
        var bounded = Delta.CreateASCII(source, target, DeltaSearchMode.Bounded);
        Assert.StartsWith(copy, Encoding.ASCII.GetString(exhaustive.Data.Span));
        Assert.StartsWith(decoys == 255 ? copy : insert, Encoding.ASCII.GetString(bounded.Data.Span));
    }

    [Theory]
    [InlineData("Binary", 255)]
    [InlineData("Binary", 256)]
    [InlineData("ASCII", 255)]
    [InlineData("ASCII", 256)]
    [InlineData("Fossil", 255)]
    [InlineData("Fossil", 256)]
    public void BoundedSearchIncludesCandidate256ButStopsBefore257(string format, int decoys)
    {
        var (source, target) = LateMatch(decoys);
        var exhaustive = Create(format, source, target, DeltaSearchMode.Exhaustive);
        var bounded = Create(format, source, target, DeltaSearchMode.Bounded);

        Assert.True(exhaustive.Data.Length < 128);
        if (decoys == 255)
            Assert.Equal(exhaustive.Data.ToArray(), bounded.Data.ToArray());
        else
            Assert.True(bounded.Data.Length > target.Length);

        var codec = Codec(format);
        Assert.Equal(target, codec.Apply.Apply(source, exhaustive.Data.Span).Data.ToArray());
        Assert.Equal(target, codec.Apply.Apply(source, bounded.Data.Span).Data.ToArray());

        // Selecting bounded mode for one call must not change the existing default.
        Assert.Equal(exhaustive.Data.ToArray(), CreateDefault(format, source, target).Data.ToArray());
        Assert.Equal(exhaustive.Data.ToArray(), codec.Create.Create(source, target).Data.ToArray());
        Assert.Equal(bounded.Data.ToArray(), Codec(format, DeltaSearchMode.Bounded).Create.Create(source, target).Data.ToArray());
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void BoundedModeRoundTripsEmptyShortAndEditedInputs(string format)
    {
        var random = new Random(11);
        foreach (var length in new[] { 0, 1, 7, 8, 16, 17, 1020, 2047, 2048, 524288 })
        {
            var source = new byte[length];
            random.NextBytes(source);
            var target = (byte[])source.Clone();
            for (var i = 0; i < target.Length; i += 97)
                target[i] ^= 0xff;

            var patch = Create(format, source, target, DeltaSearchMode.Bounded);
            Assert.Equal(target, Codec(format).Apply.Apply(source, patch.Data.Span).Data.ToArray());
        }

        var unrelatedTarget = new byte[128];
        random.NextBytes(unrelatedTarget);
        var insert = Create(format, Array.Empty<byte>(), unrelatedTarget, DeltaSearchMode.Bounded);
        Assert.Equal(unrelatedTarget, Codec(format).Apply.Apply(Array.Empty<byte>(), insert.Data.Span).Data.ToArray());
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void RepetitiveChangesRoundTripInBoundedMode(string format)
    {
        var source = new byte[16384];
        var target = new byte[source.Length];
        for (var i = 8; i < target.Length; i += 9)
            target[i] = 1;

        var patch = Create(format, source, target, DeltaSearchMode.Bounded);
        Assert.Equal(target, Codec(format).Apply.Apply(source, patch.Data.Span).Data.ToArray());
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void StaticModesCanBeUsedConcurrently(string format)
    {
        var (source, target) = LateMatch(256);
        var exhaustive = Create(format, source, target, DeltaSearchMode.Exhaustive).Data.ToArray();
        var bounded = Create(format, source, target, DeltaSearchMode.Bounded).Data.ToArray();

        Parallel.For(0, 8, i =>
        {
            var mode = i % 2 == 0 ? DeltaSearchMode.Exhaustive : DeltaSearchMode.Bounded;
            var expected = mode == DeltaSearchMode.Exhaustive ? exhaustive : bounded;
            Assert.Equal(expected, Create(format, source, target, mode).Data.ToArray());
        });
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void UnknownModesAreRejectedEvenForEmptyTargets(string format)
    {
        var invalid = (DeltaSearchMode)123;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(format, Array.Empty<byte>(), Array.Empty<byte>(), invalid));
        Assert.Equal("searchMode", error.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => Codec(format, invalid));
    }

    [Fact]
    public void GenericCreatorSupportsPerCallOverridesWithoutChangingItsDefault()
    {
        var (source, target) = LateMatch(256);
        var creator = new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum(), DeltaSearchMode.Bounded);
        var bounded = creator.Create(source, target);
        var exhaustive = creator.Create(source, target, DeltaSearchMode.Exhaustive);

        Assert.True(bounded.Data.Length > target.Length);
        Assert.True(exhaustive.Data.Length < 128);
        Assert.Equal(bounded.Data.ToArray(), creator.Create(source, target).Data.ToArray());
        Assert.Equal(exhaustive.Data.ToArray(), new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>().Create(source, target).Data.ToArray());
        Assert.Equal(exhaustive.Data.ToArray(), new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum()).Create(source, target).Data.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => creator.Create(source, target, (DeltaSearchMode)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeltaCreate<BinaryDeltaWriter, DeltaChecksum>(new DeltaChecksum(), (DeltaSearchMode)(-1)));
    }

    private static (byte[] Source, byte[] Target) LateMatch(int decoys)
    {
        var target = new byte[4096];
        var source = new byte[decoys * 16 + target.Length];
        for (var i = 0; i < decoys; i++)
            source.AsSpan(i * 16 + 8, 8).Fill(1);
        return (source, target);
    }

    private static IDelta Codec(string format, DeltaSearchMode mode) => format switch
    {
        "Binary" => new DeltaBinary(mode),
        "ASCII" => new DeltaASCII(mode),
        "Fossil" => new DeltaFossil(mode),
        _ => throw new ArgumentException(nameof(format))
    };

    private static IDelta Codec(string format) => format switch
    {
        "Binary" => new DeltaBinary(),
        "ASCII" => new DeltaASCII(),
        "Fossil" => new DeltaFossil(),
        _ => throw new ArgumentException(nameof(format))
    };

    private static DeltaResult Create(string format, byte[] source, byte[] target, DeltaSearchMode mode) => format switch
    {
        "Binary" => Delta.CreateBinary(source, target, mode),
        "ASCII" => Delta.CreateASCII(source, target, mode),
        "Fossil" => Delta.CreateFossil(source, target, mode),
        _ => throw new ArgumentException(nameof(format))
    };

    private static DeltaResult CreateDefault(string format, byte[] source, byte[] target) => format switch
    {
        "Binary" => Delta.CreateBinary(source, target),
        "ASCII" => Delta.CreateASCII(source, target),
        "Fossil" => Delta.CreateFossil(source, target),
        _ => throw new ArgumentException(nameof(format))
    };
}
