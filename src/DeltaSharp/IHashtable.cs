namespace DeltaSharp;

interface IHashtable
{
    int Bucket(ulong x);
    int Match(ReadOnlySpan<byte> data);
    int More(int pos);
}