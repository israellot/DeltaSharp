using System.Runtime.InteropServices;
using DeltaSharp;
using DeltaSharp.Format;

namespace UnitTests;

public class WriterCapacityTests
{
    [Theory]
    [InlineData("Binary")]
    [InlineData("ASCII")]
    [InlineData("Fossil")]
    public void BuiltInWritersAvoidRetainingExcessCapacity(string format)
    {
        var codec = AuditRegressionTests.Format(format);
        var random = new Random(71237);
        var source = new byte[65536];
        random.NextBytes(source);

        var patch = codec.Create.Create(source, source).Data;
        Assert.True(MemoryMarshal.TryGetArray(patch, out var small));
        Assert.InRange(small.Array!.Length, patch.Length, 4096);

        var target = new byte[source.Length];
        random.NextBytes(target);
        patch = codec.Create.Create(source, target).Data;
        Assert.True(MemoryMarshal.TryGetArray(patch, out var large));
        Assert.InRange(large.Array!.Length, patch.Length, patch.Length + 13);
        Assert.Equal(target, codec.Apply.Apply(source, patch.Span).Data.ToArray());
    }

    // A custom value-type writer can carry state in fields through every command and disposal.
    private struct CountingWriter : IDeltaWriter
    {
        private readonly byte[] result;
        private int commands;

        public CountingWriter(int sizeHint)
        {
            result = new byte[6];
            BitConverter.GetBytes(sizeHint).CopyTo(result, 0);
            commands = 0;
        }

        public void WriteLength(ulong length) => commands++;
        public void WriteCopy(ulong position, ulong length) => commands++;
        public void WriteInsert(ReadOnlySpan<byte> data) => commands++;
        public void WriteChecksum(uint checksum) => commands++;
        public ReadOnlyMemory<byte> GetOutput()
        {
            result[4] = (byte)commands;
            return result;
        }
        public void Dispose() => result[5] = (byte)commands;
    }

    [Fact]
    public void CustomWriterReceivesOriginalHintAndKeepsStateAcrossDispatch()
    {
        var data = new byte[32768];
        new Random(11).NextBytes(data);
        var result = new DeltaCreate<CountingWriter, DeltaChecksum>().Create(data, data).Data.ToArray();
        Assert.Equal(data.Length / 2, BitConverter.ToInt32(result, 0));
        Assert.Equal(3, result[4]); // Length, COPY, checksum.
        Assert.Equal(result[4], result[5]);
    }
}
