namespace Paddock.Domain.Random;

/// <summary>
/// FNV-1a 64-bit. Stable across processes, unlike <see cref="string.GetHashCode"/>.
/// </summary>
internal static class Fnv1a64
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Hash(ReadOnlySpan<byte> data)
    {
        unchecked
        {
            ulong hash = Offset;
            for (var i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= Prime;
            }

            return hash;
        }
    }
}
