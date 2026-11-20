namespace DeltaSharp.Util.Hashtable;

internal interface IHashtable
{
    int Match(ReadOnlySpan<byte> data);
    int More(int pos);
}