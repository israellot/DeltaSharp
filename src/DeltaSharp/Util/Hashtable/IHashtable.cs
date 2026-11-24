namespace DeltaSharp.Util.Hashtable;

internal interface IHashtable
{
    // Every candidate consumes one search slot, including candidates whose tag rejects them.
    int FirstCandidate(ReadOnlySpan<byte> data, out int tag, out bool mayMatch);
    int NextCandidate(int pos, int tag, out bool mayMatch);
}