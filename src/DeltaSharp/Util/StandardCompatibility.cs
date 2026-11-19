#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace DeltaSharp.Util
{
    internal static class StandardCompatibility
    {
        // The delta writers own expandable MemoryStreams and only append to them.
        public static void Write(this MemoryStream stream, ReadOnlySpan<byte> bytes)
        {
            int offset = checked((int)stream.Position);
            int end = checked(offset + bytes.Length);
            stream.SetLength(end);
            bytes.CopyTo(stream.GetBuffer().AsSpan(offset));
            stream.Position = end;
        }

        public static int Count<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            int matches = 0;
            foreach (T item in span)
                if (item.Equals(value))
                    matches++;
            return matches;
        }
    }
}
#endif
